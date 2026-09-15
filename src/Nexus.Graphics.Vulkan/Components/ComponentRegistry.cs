using System.Dynamic;

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
    private readonly Dictionary<ResourceId, RenderItem> _renderItems = [];

    private readonly IGeometryFactory _geometryFactory = geometryFactory;
    private readonly IShaderFactory _shaderFactory = shaderFactory;
    private readonly IPipelineRegistry _pipelineRegistry = pipelineRegistry;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly ILogger<ComponentRegistry> _logger = logger;

    public bool CanLoad(IGraphicsComponent component) =>
        component switch
        {
            UniformColorMeshRenderer => true,
            _ => false,
        };

    public RenderItem[] Load(IGraphicsComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.TryGetValue(component.Id, out var cached))
            return cached;

        var renderItems = GetOrCreateRenderItems(component);

        foreach (var item in renderItems)
        {
            item.Add(component);
        }

        _components[component.Id] = renderItems;

        return renderItems;
    }

    private RenderItem[] GetOrCreateRenderItems(IGraphicsComponent component)
    {
        return component switch
        {
            UniformColorMeshRenderer renderer => [GetOrCreateRenderItem(renderer)],

            _ => throw new NotSupportedException(
                $"Unsupported graphics component: {component.GetType().Name}"
            ),
        };
    }

    private RenderItem GetOrCreateRenderItem(UniformColorMeshRenderer component)
    {
        var geometry =
            component.Geometry
            ?? throw new InvalidOperationException(
                $"{nameof(UniformColorMeshRenderer)} requires geometry."
            );

        var renderItemId = new IdentityHashBuilder(nameof(UniformColorMeshRenderer))
            .Add(geometry.Id)
            .Add(RenderPasses.Main)
            .Compute();

        if (_renderItems.TryGetValue(renderItemId, out var existing))
            return existing;

        var renderItem = Create(component, renderItemId);

        _renderItems[renderItemId] = renderItem;

        return renderItem;
    }

    private RenderItem Create(UniformColorMeshRenderer component, ResourceId id)
    {
        // TODO: We should be able to use IGeometry.GetData but it hasn't been implemented yet
        if (component.Geometry is not UniformColorVertexGeometry geometry)
        {
            throw new NotSupportedException(
                $"{nameof(UniformColorMeshRenderer)} currently requires "
                    + $"{nameof(UniformColorVertexGeometry)} geometry."
            );
        }

        var geometryDefinition = new UniformColorVertexGeometryDefinition([.. geometry.Vertices]);

        var geometryId = _geometryFactory.Create(geometryDefinition);

        var vertexShader = ShaderDescriptions.UniformColorVertexShader;
        var fragmentShader = ShaderDescriptions.UniformColorFragmentShader;

        _shaderFactory.Create(vertexShader);
        _shaderFactory.Create(fragmentShader);

        var mainPassIndex = RenderPasses.GetIndex(RenderPasses.Main);

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(
            new PipelineDefinitionBuilder(UNIFORM_COLOR_PIPELINE_NAME)
                .WithShader(vertexShader)
                .WithShader(fragmentShader)
                .WithRenderPass(_swapChain.Passes[mainPassIndex])
                .WithVertexDescription(vertexShader.VertexDescription)
                .WithTopology(vertexShader.Topology)
                .WithDepthTest(false)
                .WithDepthWrite(false)
                .WithCullMode(CullModeFlags.None)
                .Build()
        );

        return new RenderItem
        {
            Id = id,
            RenderMask = RenderPasses.Main,
            Pipeline = pipeline,
            Layout = layout,
            VertexBuffer = _geometryFactory.ReadBuffer(geometryId),
            VertexCount = _geometryFactory.ReadVertexCount(geometryId),
        };
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
