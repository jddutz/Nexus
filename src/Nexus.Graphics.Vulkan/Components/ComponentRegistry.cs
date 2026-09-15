namespace Nexus.Graphics.Vulkan.Components;

public class ComponentRegistry(
    IGeometryFactory geometryFactory,
    IShaderFactory shaderFactory,
    IPipelineRegistry pipelineRegistry,
    ISwapChain swapChain,
    ILogger<ComponentRegistry> logger
) : IComponentRegistry
{
    private const string UNIFORM_COLOR_PIPELINE_NAME = "UniformColorMesh";
    private readonly Dictionary<ComponentId, RenderItem[]> _components = [];

    private readonly IGeometryFactory _geometryFactory = geometryFactory;
    private readonly IShaderFactory _shaderFactory = shaderFactory;
    private readonly IPipelineRegistry _pipelineRegistry = pipelineRegistry;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly ILogger<ComponentRegistry> _logger = logger;

    public bool CanLoad(IComponent component)
    {
        var canLoad = component is UniformColorMeshRenderer;

        _logger.LogDebug(
            "Checked whether Vulkan component registry can load a component. "
                + "ComponentId={ComponentId}, ComponentType={ComponentType}, CanLoad={CanLoad}",
            component.Id,
            component.GetType().Name,
            canLoad
        );

        return canLoad;
    }

    public RenderItem[] Load(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.TryGetValue(component.Id, out var existing))
        {
            _logger.LogDebug(
                "Vulkan graphics component is already loaded. "
                    + "ComponentId={ComponentId}, ComponentType={ComponentType}, RenderItemCount={RenderItemCount}",
                component.Id,
                component.GetType().Name,
                existing.Length
            );

            return existing;
        }

        _logger.LogDebug(
            "Loading Vulkan graphics component. "
                + "ComponentId={ComponentId}, ComponentType={ComponentType}",
            component.Id,
            component.GetType().Name
        );

        RenderItem[] items = component switch
        {
            UniformColorMeshRenderer renderer => Load(renderer),

            _ => throw new NotSupportedException(
                $"Unsupported graphics component: {component.GetType().Name}"
            ),
        };

        _components.Add(component.Id, items);

        _logger.LogInformation(
            "Loaded Vulkan graphics component. "
                + "ComponentId={ComponentId}, ComponentType={ComponentType}, "
                + "RenderItemCount={RenderItemCount}, LoadedComponentCount={LoadedComponentCount}",
            component.Id,
            component.GetType().Name,
            items.Length,
            _components.Count
        );

        return items;
    }

    private RenderItem[] Load(UniformColorMeshRenderer component)
    {
        if (component.Geometry is not VertexGeometryResourceDescription geometry)
        {
            throw new NotSupportedException(
                $"{nameof(UniformColorMeshRenderer)} currently requires "
                    + $"{nameof(VertexGeometryResourceDescription)} geometry."
            );
        }

        var geometryDefinition = new UniformColorVertexGeometryDefinition([.. geometry.Vertices]);

        var geometryId = _geometryFactory.Create(geometryDefinition);

        _logger.LogDebug(
            "Created or reused Vulkan component geometry. "
                + "ComponentId={ComponentId}, GeometryId={GeometryId}, VertexCount={VertexCount}",
            component.Id,
            geometryId,
            geometry.Vertices.Length
        );

        var vertexShader = ResourceDefinitions.UniformColorVertexShader;
        var fragmentShader = ResourceDefinitions.UniformColorFragmentShader;

        _shaderFactory.Create(vertexShader);
        _shaderFactory.Create(fragmentShader);

        _logger.LogDebug(
            "Created or reused Vulkan component shaders. "
                + "ComponentId={ComponentId}, VertexShader={VertexShader}, FragmentShader={FragmentShader}",
            component.Id,
            vertexShader,
            fragmentShader
        );

        var mainPassIndex = RenderPasses.GetIndex(RenderPasses.Main);

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(
            new PipelineDefinitionBuilder(UNIFORM_COLOR_PIPELINE_NAME)
                .WithShader(vertexShader)
                .WithShader(fragmentShader)
                .WithRenderPass(_swapChain.Passes[mainPassIndex])
                .WithVertexBinding(
                    new VertexInputBindingDescription
                    {
                        Binding = 0,
                        Stride = (uint)Unsafe.SizeOf<Vertex>(),
                        InputRate = VertexInputRate.Vertex,
                    }
                )
                .WithVertexAttribute(
                    new VertexInputAttributeDescription
                    {
                        Location = 0,
                        Binding = 0,
                        Format = Format.R32G32B32Sfloat,
                        Offset = 0,
                    }
                )
                .WithDepthTest(false)
                .WithDepthWrite(false)
                .WithCullMode(CullModeFlags.None)
                .Build()
        );

        _logger.LogDebug(
            "Created or reused Vulkan component pipeline. ComponentId={ComponentId}, "
                + "PipelineName={PipelineName}, PipelineHandle={PipelineHandle}, "
                + "PipelineLayoutHandle={PipelineLayoutHandle}, RenderPassName={RenderPassName}",
            component.Id,
            UNIFORM_COLOR_PIPELINE_NAME,
            pipeline.Handle,
            layout.Handle,
            RenderPasses.GetName(RenderPasses.Main)
        );

        var renderItem = new RenderItem
        {
            RenderMask = RenderPasses.Main,
            Pipeline = pipeline,
            Layout = layout,
            VertexBuffer = _geometryFactory.ReadBuffer(geometryId),
            VertexCount = _geometryFactory.ReadVertexCount(geometryId),
        };

        _logger.LogDebug(
            "Built Vulkan render item for component. "
                + "ComponentId={ComponentId}, GeometryId={GeometryId}, "
                + "PipelineName={PipelineName}, VertexCount={VertexCount}",
            component.Id,
            geometryId,
            UNIFORM_COLOR_PIPELINE_NAME,
            renderItem.VertexCount
        );

        return [renderItem];
    }

    public bool CanUnload(ComponentId componentId)
    {
        var canUnload = _components.ContainsKey(componentId);

        _logger.LogDebug(
            "Checked whether Vulkan component registry can unload a component. "
                + "ComponentId={ComponentId}, CanUnload={CanUnload}",
            componentId,
            canUnload
        );

        return canUnload;
    }

    public void Unload(ComponentId componentId)
    {
        if (!_components.Remove(componentId))
        {
            _logger.LogDebug(
                "Vulkan graphics component unload skipped because the component is not loaded. "
                    + "ComponentId={ComponentId}",
                componentId
            );
            return;
        }

        _logger.LogDebug(
            "Unloaded Vulkan graphics component. ComponentId={ComponentId}, "
                + "RemainingComponentCount={RemainingComponentCount}",
            componentId,
            _components.Count
        );

        // Do not delete geometry/shaders/pipelines here yet.
        // They may be shared by other component realizations.
        // Resource lifetime/ref-counting can be handled separately.
    }
}
