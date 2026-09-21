namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Tracks a loaded component's owning render items alongside the component reference itself, so
/// <see cref="ComponentRegistry.Unload"/> can unsubscribe from <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// without requiring the caller to supply the component again.
/// </summary>
/// <param name="Component">The loaded component.</param>
/// <param name="RenderItems">The render items to which the component contributes instance data.</param>
internal sealed record ComponentRegistration(
    IGraphicsComponent Component,
    RenderItem[] RenderItems
);

/// <summary>
/// Creates and maintains Vulkan render-item registrations for supported graphics components.
/// </summary>
[Obsolete("Use the newer Vulkan graphics resource registries.")]
public class ComponentRegistry(
    Context context,
    IVertexBufferRegistry vertexBufferRegistry,
    IBufferManager bufferManager,
    IShaderFactory shaderFactory,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    ICameraRegistry cameraRegistry,
    ITextureRegistry textureRegistry,
    ISwapChain swapChain,
    IRenderer renderer
) : IComponentRegistry
{
    private readonly Context _context = context;
    private const string UNIFORM_COLOR_PIPELINE_NAME = "UniformColorMesh";
    private const string TEXTURED_QUAD_PIPELINE_NAME = "TexturedQuad";
    private readonly Dictionary<ComponentId, ComponentRegistration> _components = [];
    private readonly Dictionary<RenderableId, RenderItem> _renderItems = [];
    private readonly Dictionary<RenderableId, VkBuffer[]> _uniformBuffers = [];

    private readonly IVertexBufferRegistry _vertexBufferRegistry = vertexBufferRegistry;
    private readonly IBufferManager _bufferManager = bufferManager;
    private readonly IShaderFactory _shaderFactory = shaderFactory;
    private readonly IPipelineRegistry _pipelineRegistry = pipelineRegistry;
    private readonly IDescriptorSetPool _descriptorSetPool = descriptorSetPool;
    private readonly ICameraRegistry _cameraRegistry = cameraRegistry;
    private readonly ITextureRegistry _textureRegistry = textureRegistry;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;

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
    /// Creates or retrieves render items for the specified component, adds its packed instance
    /// records, and subscribes to its <see cref="INotifyPropertyChanged.PropertyChanged"/> event
    /// so later property changes keep its Vulkan realization synchronized.
    /// </summary>
    /// <param name="component">The graphics component to load.</param>
    /// <returns>The render items associated with the loaded component.</returns>
    public RenderItem[] Load(IGraphicsComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.TryGetValue(component.Id, out var cached))
            return cached.RenderItems;

        var renderItems = GetOrCreateRenderItems(component);

        foreach (var renderable in component.Renderables)
        {
            foreach (var item in renderItems)
                item.AddInstances(renderable);
        }

        _components[component.Id] = new ComponentRegistration(component, renderItems);
        component.PropertyChanged += OnComponentPropertyChanged;

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
        _cameraRegistry.Register(camera);

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
            component.Mesh
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
    private RenderItem Create(UniformColorMeshRenderer component, RenderableId id)
    {
        var mesh = component.Mesh;
        var vertexShader = BuiltInShaders.UniformColorVertexShader;
        var fragmentShader = BuiltInShaders.UniformColorFragmentShader;
        _shaderFactory.Create(vertexShader);
        _shaderFactory.Create(fragmentShader);

        var mainPassIndex = RenderPasses.GetIndex(RenderPasses.Main);

        var pipelineDefinition = new PipelineDefinitionBuilder(
            UNIFORM_COLOR_PIPELINE_NAME,
            _context
        )
            .WithShader(vertexShader)
            .WithShader(fragmentShader)
            .WithRenderPass(_swapChain.Passes[mainPassIndex])
            .WithTopology(vertexShader.Topology)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .WithCullMode(CullModeFlags.None)
            .Build();

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(pipelineDefinition);
        var descriptorSets = CreateDescriptorSets(pipelineDefinition, component, null);
        _uniformBuffers[id] = descriptorSets.UniformBuffers;

        return new RenderItem
        {
            Id = id,
            RenderPassMask = RenderPasses.Main,
            Pipelines = CreatePassArray(pipeline, RenderPasses.Main),
            Layouts = CreatePassArray(layout, RenderPasses.Main),
            VertexBuffers = CreatePassArray(
                _vertexBufferRegistry.Acquire(component),
                RenderPasses.Main
            ),
            DescriptorSets = CreatePassArray(descriptorSets.Sets, RenderPasses.Main),
            VertexCount = checked((uint)mesh.Source.Count),
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
            component.Mesh
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
    private RenderItem Create(TexturedQuadRenderer component, RenderableId id)
    {
        var texture =
            component.Texture
            ?? throw new InvalidOperationException(
                $"{nameof(TexturedQuadRenderer)} requires a texture."
            );

        if (!_textureRegistry.IsRegistered(texture.ContentId))
            _textureRegistry.Register(texture, texture);

        var mesh = component.Mesh;
        var vertexShader = BuiltInShaders.TexturedQuadVertexShader;
        var (pipeline, layout, pipelineDefinition) = GetOrCreateTexturedQuadPipeline();
        var descriptorSets = CreateDescriptorSets(pipelineDefinition, component, texture);
        _uniformBuffers[id] = descriptorSets.UniformBuffers;

        return new RenderItem
        {
            Id = id,
            RenderPassMask = RenderPasses.Main,
            Pipelines = CreatePassArray(pipeline, RenderPasses.Main),
            Layouts = CreatePassArray(layout, RenderPasses.Main),
            VertexBuffers = CreatePassArray(
                _vertexBufferRegistry.Acquire(component),
                RenderPasses.Main
            ),
            VertexCount = checked((uint)mesh.Source.Count),
            DescriptorSets = CreatePassArray(descriptorSets.Sets, RenderPasses.Main),
        };
    }

    private static T[] CreatePassArray<T>(T value, uint passMask)
    {
        var values = new T[RenderPasses.Count];
        values[RenderPasses.GetIndex(passMask)] = value;
        return values;
    }

    /// <summary>
    /// Gets or creates the textured-quad pipeline. Its descriptor schema contains the contract
    /// uniform buffer followed by the explicit sampled-texture descriptor set.
    /// </summary>
    /// <returns>The pipeline, its layout, and its identifier.</returns>
    private (
        Pipeline Pipeline,
        PipelineLayout Layout,
        PipelineDefinition Definition
    ) GetOrCreateTexturedQuadPipeline()
    {
        var vertexShader = BuiltInShaders.TexturedQuadVertexShader;
        var fragmentShader = BuiltInShaders.TexturedQuadFragmentShader;

        _shaderFactory.Create(vertexShader);
        _shaderFactory.Create(fragmentShader);

        var mainPassIndex = RenderPasses.GetIndex(RenderPasses.Main);

        var pipelineDefinition = new PipelineDefinitionBuilder(
            TEXTURED_QUAD_PIPELINE_NAME,
            _context
        )
            .WithShader(vertexShader)
            .WithShader(fragmentShader)
            .WithRenderPass(_swapChain.Passes[mainPassIndex])
            .WithTopology(vertexShader.Topology)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .WithCullMode(CullModeFlags.None)
            .WithDescriptorSchema(DescriptorSchemas.Textured)
            .Build();

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(pipelineDefinition);

        return (pipeline, layout, pipelineDefinition);
    }

    private (DescriptorSet[] Sets, VkBuffer[] UniformBuffers) CreateDescriptorSets(
        PipelineDefinition definition,
        IRenderable renderable,
        Texture? texture
    )
    {
        if (definition.DescriptorSchema is not { } schema)
            return ([], []);

        var contracts = new IShaderContract?[]
        {
            definition.VertexShader,
            definition.TessellationControlShader,
            definition.TessellationEvalShader,
            definition.GeometryShader,
            definition.FragmentShader,
        };
        var uniformContracts = contracts
            .Where(shader => shader?.UniformLayout.Length > 0)
            .ToArray();
        var uniformIndex = 0;
        var sets = new DescriptorSet[schema.Sets.Length];
        var buffers = new List<VkBuffer>();

        for (var setIndex = 0; setIndex < schema.Sets.Length; setIndex++)
        {
            var setSchema = schema.Sets[setIndex];
            sets[setIndex] = _descriptorSetPool.Allocate(
                _pipelineRegistry.GetDescriptorSetLayout(definition.Id, (uint)setIndex)
            );

            foreach (var binding in setSchema.Bindings)
            {
                switch (binding.DescriptorType)
                {
                    case DescriptorType.UniformBuffer:
                        if (uniformIndex >= uniformContracts.Length)
                            throw new InvalidOperationException(
                                $"Pipeline '{definition.Name}' declares a uniform descriptor without a shader contract."
                            );

                        var shader = uniformContracts[uniformIndex++]!;
                        var data = renderable.GetUniformData(shader.UniformLayout).ToArray();
                        var expectedSize = checked(
                            (ulong)shader.UniformLayout.Sum(input => input.Size)
                        );
                        if ((ulong)data.Length != expectedSize)
                            throw new InvalidOperationException(
                                $"Renderable '{renderable.GetType().Name}' supplied {data.Length} uniform bytes for shader '{shader.Name}', expected {expectedSize}."
                            );

                        var buffer = _bufferManager.CreateUniformBuffer(expectedSize);
                        _bufferManager.UpdateBuffer(buffer, data);
                        _descriptorSetPool.WriteUniformBuffer(
                            sets[setIndex],
                            binding.Binding,
                            buffer,
                            0,
                            expectedSize
                        );
                        buffers.Add(buffer);
                        break;

                    case DescriptorType.CombinedImageSampler:
                        if (texture is null)
                            throw new InvalidOperationException(
                                $"Pipeline '{definition.Name}' requires a sampled texture, but the renderable supplied none."
                            );

                        if (
                            !_textureRegistry.TryGetImageView(texture.ContentId, out var imageView)
                            || !_textureRegistry.TryGetSampler(texture.ContentId, out var sampler)
                        )
                            throw new InvalidOperationException(
                                $"Texture '{texture.Id}' has no realized Vulkan image view and sampler."
                            );

                        _descriptorSetPool.WriteCombinedImageSampler(
                            sets[setIndex],
                            binding.Binding,
                            imageView,
                            sampler
                        );
                        break;
                }
            }
        }

        if (uniformIndex != uniformContracts.Length)
            throw new InvalidOperationException(
                $"Pipeline '{definition.Name}' has {uniformContracts.Length} uniform shader contracts but only {uniformIndex} uniform descriptor bindings."
            );

        return (sets, [.. buffers]);
    }

    /// <summary>
    /// Determines whether this registry has loaded the specified component.
    /// </summary>
    /// <param name="componentId">The identifier of the component to evaluate.</param>
    /// <returns><see langword="true"/> when the component is loaded; otherwise, <see langword="false"/>.</returns>
    public bool CanUnload(ComponentId componentId)
    {
        return _components.ContainsKey(componentId);
    }

    /// <summary>
    /// Unsubscribes from the component's <see cref="INotifyPropertyChanged.PropertyChanged"/> event
    /// and removes its instance records from its associated render items.
    /// </summary>
    /// <param name="componentId">The identifier of the component to unload.</param>
    public void Unload(ComponentId componentId)
    {
        if (!_components.Remove(componentId, out var registration))
            return;

        registration.Component.PropertyChanged -= OnComponentPropertyChanged;

        foreach (var item in registration.RenderItems)
        foreach (var renderable in registration.Component.Renderables)
            item.RemoveInstances(renderable.Id);

        // Safe no-op when componentId does not identify a registered camera.
        _cameraRegistry.Remove(componentId);

        // Do not delete geometry/shaders/pipelines here yet.
        // They may be shared by other component realizations.
        // Resource lifetime/ref-counting can be handled separately.
    }

    /// <summary>
    /// Routes a loaded component's property-change notification to its Vulkan update handling.
    /// </summary>
    /// <param name="sender">The component that raised the notification.</param>
    /// <param name="e">The event data describing which property changed.</param>
    private void OnComponentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is IGraphicsComponent component)
            Update(component, e.PropertyName);
    }

    /// <summary>
    /// Dispatches a component's property change to the update handling appropriate for its type.
    /// </summary>
    /// <param name="component">The component that changed.</param>
    /// <param name="propertyName">The name of the property that changed, or <see langword="null"/> when unknown.</param>
    private void Update(IGraphicsComponent component, string? propertyName)
    {
        switch (component)
        {
            case ICameraComponent camera:
                _cameraRegistry.Update(camera);
                break;

            case UniformColorMeshRenderer renderer:
                Update(renderer, propertyName);
                break;

            case TexturedQuadRenderer renderer:
                Update(renderer, propertyName);
                break;
        }
    }

    /// <summary>
    /// Updates a uniform-color mesh renderer's instance data, or recreates its render-item
    /// registration when the changed property affects the item's identity.
    /// </summary>
    /// <param name="component">The renderer that changed.</param>
    /// <param name="propertyName">The name of the property that changed, or <see langword="null"/> when unknown.</param>
    private void Update(UniformColorMeshRenderer component, string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(UniformColorMeshRenderer.TransformationMatrix):
            case nameof(UniformColorMeshRenderer.Color):
                UpdateInstances(component);
                break;
            case nameof(UniformColorMeshRenderer.View):
                UpdateUniformData(component);
                break;

            // Geometry affects render-item identity; an unknown/null property name is handled
            // conservatively the same way, since it may represent any property.
            default:
                Recreate(component);
                break;
        }
    }

    /// <summary>
    /// Updates a textured quad renderer's instance data, or recreates its render-item
    /// registration when the changed property affects the item's identity.
    /// </summary>
    /// <param name="component">The renderer that changed.</param>
    /// <param name="propertyName">The name of the property that changed, or <see langword="null"/> when unknown.</param>
    private void Update(TexturedQuadRenderer component, string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(TexturedQuadRenderer.TransformationMatrix):
            case nameof(TexturedQuadRenderer.TextureRegion):
            case nameof(TexturedQuadRenderer.Color):
                UpdateInstances(component);
                break;
            case nameof(TexturedQuadRenderer.View):
                UpdateUniformData(component);
                break;

            // Texture affects render-item identity; an unknown/null property name is handled
            // conservatively the same way, since it may represent any property.
            default:
                Recreate(component);
                break;
        }
    }

    /// <summary>
    /// Replaces a component's existing instance records. No buffer upload,
    /// descriptor update, or command recording happens until the next frame is rendered.
    /// </summary>
    /// <param name="component">The renderable component whose instance record should be repacked.</param>
    private void UpdateInstances(IGraphicsComponent component)
    {
        if (!_components.TryGetValue(component.Id, out var registration))
            return;

        foreach (var renderable in component.Renderables)
        foreach (var item in registration.RenderItems)
            item.UpdateInstances(renderable);
    }

    /// <summary>Updates the contract-driven uniform buffers for a renderable component.</summary>
    /// <param name="component">The component whose uniform data changed.</param>
    private void UpdateUniformData(IGraphicsComponent component)
    {
        if (!_components.TryGetValue(component.Id, out var registration))
            return;

        foreach (var renderable in component.Renderables)
        foreach (var item in registration.RenderItems)
        {
            if (!_uniformBuffers.TryGetValue(item.Id, out var buffers))
                continue;
            var contracts = new IShaderContract?[]
            {
                renderable.VertexShader,
                renderable.FragmentShader,
            };
            var uniformContracts = contracts
                .Where(shader => shader?.UniformLayout.Length > 0)
                .Select(shader => shader!)
                .ToArray();
            if (buffers.Length != uniformContracts.Length)
                throw new InvalidOperationException(
                    $"Render item uniform buffer count {buffers.Length} does not match shader contract count {uniformContracts.Length}."
                );

            if (buffers.Length == 0)
                continue;

            for (var index = 0; index < buffers.Length; index++)
            {
                var layout = uniformContracts[index].UniformLayout;
                var data = renderable.GetUniformData(layout).ToArray();
                var expectedSize = checked((ulong)layout.Sum(input => input.Size));
                if ((ulong)data.Length != expectedSize)
                    throw new InvalidOperationException(
                        $"Renderable '{renderable.GetType().Name}' supplied {data.Length} uniform bytes for shader '{uniformContracts[index].Name}', expected {expectedSize}."
                    );

                _bufferManager.UpdateBuffer(buffers[index], data);
            }
        }
    }

    /// <summary>
    /// Moves all of a component's instance records from its previous render items to the render items
    /// matching its current state, creating new render items when no existing one matches.
    /// </summary>
    /// <param name="component">The renderable component whose render-item registration should be recreated.</param>
    private void Recreate(IGraphicsComponent component)
    {
        if (!_components.TryGetValue(component.Id, out var registration))
            return;

        foreach (var item in registration.RenderItems)
        foreach (var renderable in component.Renderables)
            item.RemoveInstances(renderable.Id);

        var renderItems = GetOrCreateRenderItems(component);

        foreach (var renderable in component.Renderables)
        foreach (var item in renderItems)
            item.AddInstances(renderable);

        _components[component.Id] = registration with { RenderItems = renderItems };

        AddNewRenderItemsToActiveLayer(renderItems);
    }

    /// <summary>
    /// Adds any of the specified render items that are not already present to the active
    /// render layer. A no-op for render items that were already shared by another component.
    /// </summary>
    /// <param name="renderItems">The render items to ensure are present in the active layer.</param>
    private void AddNewRenderItemsToActiveLayer(RenderItem[] renderItems)
    {
        var layer = _renderer.Layers[0];
        var newRenderItems = renderItems.Where(item => !layer.Items.Contains(item)).ToArray();

        if (newRenderItems.Length == 0)
            return;

        layer.Items = [.. layer.Items, .. newRenderItems];
    }
}
