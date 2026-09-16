namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Creates and maintains Vulkan render-item registrations for supported graphics components.
/// </summary>
public class ComponentRegistry(
    IGeometryFactory geometryFactory,
    IShaderFactory shaderFactory,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    ICameraRegistry cameraRegistry,
    TextureRegistry textureRegistry,
    ISwapChain swapChain,
    ILogger<ComponentRegistry> logger
) : IComponentRegistry
{
    private const string UNIFORM_COLOR_PIPELINE_NAME = "UniformColorMesh";
    private const string TEXTURED_QUAD_PIPELINE_NAME = "TexturedQuad";
    private readonly Dictionary<ComponentId, RenderItem[]> _components = [];
    private readonly Dictionary<ResourceId, RenderItem> _renderItems = [];

    private readonly IGeometryFactory _geometryFactory = geometryFactory;
    private readonly IShaderFactory _shaderFactory = shaderFactory;
    private readonly IPipelineRegistry _pipelineRegistry = pipelineRegistry;
    private readonly IDescriptorSetPool _descriptorSetPool = descriptorSetPool;
    private readonly ICameraRegistry _cameraRegistry = cameraRegistry;
    private readonly TextureRegistry _textureRegistry = textureRegistry;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly ILogger<ComponentRegistry> _logger = logger;

    /// <summary>
    /// Determines whether this registry can create render items for the specified component.
    /// </summary>
    /// <param name="component">The graphics component to evaluate.</param>
    /// <returns><see langword="true"/> when the component type is supported; otherwise, <see langword="false"/>.</returns>
    public bool CanLoad(IGraphicsComponent component) =>
        component switch
        {
            ICameraComponent => true,
            UniformColorMeshRenderer => true,
            TexturedQuadRenderer => true,
            _ => false,
        };

    /// <summary>
    /// Creates or retrieves render items for the specified component and adds its packed instance records.
    /// </summary>
    /// <param name="component">The graphics component to load.</param>
    /// <returns>The render items associated with the loaded component.</returns>
    public RenderItem[] Load(IGraphicsComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.TryGetValue(component.Id, out var cached))
            return cached;

        var renderItems = GetOrCreateRenderItems(component);

        if (component is IRenderableComponent renderable)
        {
            foreach (var item in renderItems)
            {
                item.AddInstance(renderable);
            }
        }

        _components[component.Id] = renderItems;

        return renderItems;
    }

    /// <summary>
    /// Gets the render items to which the specified component contributes instance data.
    /// </summary>
    /// <param name="component">The supported graphics component.</param>
    /// <returns>The render items for the component.</returns>
    /// <exception cref="NotSupportedException">Thrown when the component type is not supported.</exception>
    private RenderItem[] GetOrCreateRenderItems(IGraphicsComponent component)
    {
        return component switch
        {
            ICameraComponent camera => ActivateCamera(camera),
            UniformColorMeshRenderer renderer => [GetOrCreateRenderItem(renderer)],
            TexturedQuadRenderer renderer => [GetOrCreateRenderItem(renderer)],

            _ => throw new NotSupportedException(
                $"Unsupported graphics component: {component.GetType().Name}"
            ),
        };
    }

    /// <summary>
    /// Registers a camera's realized Vulkan state with <see cref="ICameraRegistry"/>. Cameras
    /// supply shared rendering state rather than drawable geometry, so activation always yields
    /// an empty render-item array.
    /// </summary>
    /// <param name="camera">The camera component to activate.</param>
    /// <returns>An empty render-item array.</returns>
    private RenderItem[] ActivateCamera(ICameraComponent camera)
    {
        var (_, _, pipelineId) = GetOrCreateTexturedQuadPipeline();
        var cameraDescriptorSetLayout = _pipelineRegistry.GetDescriptorSetLayout(pipelineId, 0);

        _cameraRegistry.Register(camera, cameraDescriptorSetLayout);

        return [];
    }

    /// <summary>
    /// Gets the shared render item for a uniform-color mesh renderer, creating it when needed.
    /// </summary>
    /// <param name="component">The mesh renderer whose geometry determines the render item.</param>
    /// <returns>The matching shared render item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the renderer has no geometry.</exception>
    private RenderItem GetOrCreateRenderItem(UniformColorMeshRenderer component)
    {
        var geometry =
            component.Geometry
            ?? throw new InvalidOperationException(
                $"{nameof(UniformColorMeshRenderer)} requires geometry."
            );

        // Depth stencil allows us to combine opaque objects into one RenderItem
        // but when we enable alpha / transparency we may need to reconsider the
        // computation algorithm so depth sort can be enabled

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

    /// <summary>
    /// Creates a Vulkan render item for the specified uniform-color mesh renderer.
    /// </summary>
    /// <param name="component">The mesh renderer that defines the item geometry.</param>
    /// <param name="id">The identifier of the render item to create.</param>
    /// <returns>A configured render item.</returns>
    /// <exception cref="NotSupportedException">Thrown when the renderer geometry is unsupported.</exception>
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

        var pipelineDefinition = new PipelineDefinitionBuilder(UNIFORM_COLOR_PIPELINE_NAME)
            .WithShader(vertexShader)
            .WithShader(fragmentShader)
            .WithRenderPass(_swapChain.Passes[mainPassIndex])
            .WithVertexDescription(vertexShader.VertexDescription)
            .WithInstanceDescription(VertexDescriptions.UniformColorInstance)
            .WithTopology(vertexShader.Topology)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .WithCullMode(CullModeFlags.None)
            .WithDescriptorSchema(DescriptorSchemas.Camera)
            .Build();

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(pipelineDefinition);

        return new RenderItem
        {
            Id = id,
            RenderMask = RenderPasses.Main,
            Pipeline = pipeline,
            Layout = layout,
            VertexBuffer = _geometryFactory.ReadBuffer(geometryId),
            VertexCount = _geometryFactory.ReadVertexCount(geometryId),
            DescriptorSetCount = _pipelineRegistry.GetDescriptorSetLayoutCount(
                pipelineDefinition.Id
            ),
        };
    }

    /// <summary>
    /// Gets the shared render item for a textured quad renderer, creating it when needed.
    /// </summary>
    /// <param name="component">The textured quad renderer whose geometry and texture determine the render item.</param>
    /// <returns>The matching shared render item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the renderer has no geometry or texture.</exception>
    private RenderItem GetOrCreateRenderItem(TexturedQuadRenderer component)
    {
        var geometry =
            component.Geometry
            ?? throw new InvalidOperationException(
                $"{nameof(TexturedQuadRenderer)} requires geometry."
            );

        var texture =
            component.Texture
            ?? throw new InvalidOperationException(
                $"{nameof(TexturedQuadRenderer)} requires a texture."
            );

        // TextureRegion, transform, and tint are instance data, so two components sampling
        // different regions of the same texture still land in the same RenderItem.
        var renderItemId = new IdentityHashBuilder(nameof(TexturedQuadRenderer))
            .Add(geometry.Id)
            .Add(texture.Id)
            .Add(RenderPasses.Main)
            .Compute();

        if (_renderItems.TryGetValue(renderItemId, out var existing))
            return existing;

        var renderItem = Create(component, renderItemId);

        _renderItems[renderItemId] = renderItem;

        return renderItem;
    }

    /// <summary>
    /// Creates a Vulkan render item for the specified textured quad renderer.
    /// </summary>
    /// <param name="component">The textured quad renderer that defines the item geometry and texture.</param>
    /// <param name="id">The identifier of the render item to create.</param>
    /// <returns>A configured render item.</returns>
    /// <exception cref="NotSupportedException">Thrown when the renderer geometry is unsupported.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the renderer's texture is not registered.</exception>
    private RenderItem Create(TexturedQuadRenderer component, ResourceId id)
    {
        if (component.Geometry is not TexturedVertex2dGeometry geometry)
        {
            throw new NotSupportedException(
                $"{nameof(TexturedQuadRenderer)} currently requires "
                    + $"{nameof(TexturedVertex2dGeometry)} geometry."
            );
        }

        var texture =
            component.Texture
            ?? throw new InvalidOperationException(
                $"{nameof(TexturedQuadRenderer)} requires a texture."
            );

        if (!_textureRegistry.IsRegistered(texture.Id))
        {
            throw new InvalidOperationException(
                $"Texture '{texture.Name}' must be registered with the {nameof(TextureRegistry)} "
                    + "before it can be used by a render item."
            );
        }

        var geometryDefinition = new TexturedVertex2dGeometryDefinition([.. geometry.Vertices]);

        var geometryId = _geometryFactory.Create(geometryDefinition);

        var (pipeline, layout, pipelineId) = GetOrCreateTexturedQuadPipeline();

        var materialDescriptorSetLayout = _pipelineRegistry.GetDescriptorSetLayout(pipelineId, 1);
        var descriptorSet = _descriptorSetPool.Allocate(materialDescriptorSetLayout);

        _textureRegistry.TryGetImageView(texture.Id, out var imageView);
        _textureRegistry.TryGetSampler(texture.Id, out var sampler);
        _descriptorSetPool.WriteCombinedImageSampler(descriptorSet, binding: 0, imageView, sampler);

        return new RenderItem
        {
            Id = id,
            RenderMask = RenderPasses.Main,
            Pipeline = pipeline,
            Layout = layout,
            VertexBuffer = _geometryFactory.ReadBuffer(geometryId),
            VertexCount = _geometryFactory.ReadVertexCount(geometryId),
            DescriptorSet = descriptorSet,
            DescriptorSetCount = _pipelineRegistry.GetDescriptorSetLayoutCount(pipelineId),
        };
    }

    /// <summary>
    /// Gets or creates the textured-quad pipeline. Set 0 of its descriptor schema is the camera
    /// view-projection uniform buffer shared with <see cref="ICameraRegistry"/>; set 1 is the
    /// per-render-item material (texture) descriptor set.
    /// </summary>
    /// <returns>The pipeline, its layout, and its identifier.</returns>
    private (
        Pipeline Pipeline,
        PipelineLayout Layout,
        PipelineId Id
    ) GetOrCreateTexturedQuadPipeline()
    {
        var vertexShader = ShaderDescriptions.TexturedQuadVertexShader;
        var fragmentShader = ShaderDescriptions.TexturedQuadFragmentShader;

        _shaderFactory.Create(vertexShader);
        _shaderFactory.Create(fragmentShader);

        var mainPassIndex = RenderPasses.GetIndex(RenderPasses.Main);

        var pipelineDefinition = new PipelineDefinitionBuilder(TEXTURED_QUAD_PIPELINE_NAME)
            .WithShader(vertexShader)
            .WithShader(fragmentShader)
            .WithRenderPass(_swapChain.Passes[mainPassIndex])
            .WithVertexDescription(vertexShader.VertexDescription)
            .WithInstanceDescription(VertexDescriptions.TexturedQuadInstance)
            .WithTopology(vertexShader.Topology)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .WithCullMode(CullModeFlags.None)
            .WithDescriptorSchema(DescriptorSchemas.Textured)
            .Build();

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(pipelineDefinition);

        return (pipeline, layout, pipelineDefinition.Id);
    }

    /// <summary>
    /// Determines whether this registry has loaded the specified component.
    /// </summary>
    /// <param name="componentId">The identifier of the component to evaluate.</param>
    /// <returns><see langword="true"/> when the component is loaded; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Removes the specified component's instance records from its associated render items.
    /// </summary>
    /// <param name="componentId">The identifier of the component to unload.</param>
    public void Unload(ComponentId componentId)
    {
        if (!_components.Remove(componentId, out var renderItems))
        {
            _logger.LogDebug(
                "Vulkan graphics component unload skipped because the component is not loaded. "
                    + "ComponentId={ComponentId}",
                componentId
            );
            return;
        }

        foreach (var item in renderItems)
        {
            item.RemoveInstance(componentId);
        }

        // Safe no-op when componentId does not identify a registered camera.
        _cameraRegistry.Remove(componentId);

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
