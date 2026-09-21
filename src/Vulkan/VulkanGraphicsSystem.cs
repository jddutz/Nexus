namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    IVertexBufferRegistry vertexBufferRegistry,
    IBufferManager bufferManager,
    IShaderFactory shaderFactory,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    IImageRegistry imageRegistry,
    IImageViewRegistry imageViewRegistry,
    ISamplerRegistry samplerRegistry,
    IEventHub eventHub,
    ILogger<VulkanGraphicsSystem> logger
) : IGraphicsSystem, IDisposable
{
    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;
    private readonly IVertexBufferRegistry _vertexBufferRegistry = vertexBufferRegistry;
    private readonly IBufferManager _bufferManager = bufferManager;
    private readonly IShaderFactory _shaderFactory = shaderFactory;
    private readonly IPipelineRegistry _pipelineRegistry = pipelineRegistry;
    private readonly IDescriptorSetPool _descriptorSetPool = descriptorSetPool;
    private readonly IImageRegistry _imageRegistry = imageRegistry;
    private readonly IImageViewRegistry _imageViewRegistry = imageViewRegistry;
    private readonly ISamplerRegistry _samplerRegistry = samplerRegistry;
    private readonly ILogger<VulkanGraphicsSystem> _logger = logger;
    private readonly Dictionary<DrawableId, RenderItem> _renderItems = [];
    private readonly Dictionary<DrawableId, RenderItem> _renderItemByRenderable = [];

    private bool disposedValue;

    private const string UNIFORM_COLOR_PIPELINE_NAME = "UniformColorMesh";
    private const string TEXTURED_QUAD_PIPELINE_NAME = "TexturedQuad";

    private readonly record struct SampledImage(ImageView ImageView, Sampler Sampler);

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        eventHub.Register(this);

        _logger.LogDebug(
            "Initializing Vulkan graphics system. ExistingRenderLayerCount={RenderLayerCount}",
            _renderer.Layers.Count()
        );

        _logger.LogInformation(
            "Vulkan graphics system initialized. DeviceHandle={DeviceHandle}, "
                + "SwapchainExtent={Width}x{Height}",
            _context.Device.Handle,
            _swapChain.SwapchainExtent.Width,
            _swapChain.SwapchainExtent.Height
        );
    }

    private void Activate(IDrawable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);

        var renderItemId = GetRenderItemId(renderable);
        if (!_renderItems.TryGetValue(renderItemId, out var renderItem))
        {
            renderItem = CreateRenderItem(renderable, renderItemId);
            _renderItems.Add(renderItemId, renderItem);
            AddRenderItem(renderItem);
        }

        renderItem.AddInstances(renderable);
        _renderItemByRenderable[renderable.Id] = renderItem;

        _logger.LogDebug(
            "Activated renderable. RenderableType={RenderableType}, RenderItemId={RenderItemId}",
            renderable.GetType().Name,
            renderItemId
        );
    }

    private static DrawableId GetRenderItemId(IDrawable renderable)
    {
        var builder = new IdentityHashBuilder(renderable.GetType().Name)
            .Add(renderable.Mesh.Id)
            .Add(RenderPasses.Main);

        if (renderable.Texture is { } texture)
            builder.Add(texture.ContentId);

        builder.Add(renderable.SamplingBehavior.Id);

        return builder.Compute();
    }

    private RenderItem CreateRenderItem(IDrawable renderable, DrawableId id)
    {
        return renderable switch
        {
            UniformColorMeshRenderer uniformColor => CreateRenderItem(uniformColor, id),
            TexturedQuadRenderer texturedQuad => CreateRenderItem(texturedQuad, id),
            _ => throw new NotSupportedException(
                $"Unsupported renderable: {renderable.GetType().Name}"
            ),
        };
    }

    private RenderItem CreateRenderItem(UniformColorMeshRenderer renderable, DrawableId id)
    {
        var vertexShader = BuiltInShaders.UniformColorVertexShader;
        var fragmentShader = BuiltInShaders.UniformColorFragmentShader;
        _shaderFactory.Create(vertexShader);
        _shaderFactory.Create(fragmentShader);

        var pipelineDefinition = new PipelineDefinitionBuilder(
            UNIFORM_COLOR_PIPELINE_NAME,
            _context
        )
            .WithShader(vertexShader)
            .WithShader(fragmentShader)
            .WithRenderPass(_swapChain.Passes[RenderPasses.GetIndex(RenderPasses.Main)])
            .WithTopology(vertexShader.Topology)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .WithCullMode(CullModeFlags.None)
            .Build();

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(pipelineDefinition);
        var descriptorSets = CreateDescriptorSets(pipelineDefinition, renderable, null);

        return new RenderItem
        {
            Id = id,
            RenderPassMask = RenderPasses.Main,
            Pipelines = CreatePassArray(pipeline, RenderPasses.Main),
            Layouts = CreatePassArray(layout, RenderPasses.Main),
            VertexBuffers = CreatePassArray(
                _vertexBufferRegistry.Acquire(renderable),
                RenderPasses.Main
            ),
            DescriptorSets = CreatePassArray(descriptorSets.Sets, RenderPasses.Main),
            VertexCount = checked((uint)renderable.Mesh.Count),
        };
    }

    private RenderItem CreateRenderItem(TexturedQuadRenderer renderable, DrawableId id)
    {
        var texture =
            renderable.Texture
            ?? throw new InvalidOperationException(
                $"{nameof(TexturedQuadRenderer)} requires a texture."
            );

        const ColorFormatEnum colorFormat = ColorFormatEnum.RGBA8UNorm;
        var format = colorFormat.ToVulkanFormat();
        var imageId = GetImageResourceId(texture, colorFormat);
        var imageViewId = GetImageViewResourceId(imageId, format);
        var samplingBehavior = renderable.SamplingBehavior;
        var samplerId = GetSamplerResourceId(samplingBehavior);
        var imageAcquired = false;
        var imageViewAcquired = false;
        var samplerAcquired = false;
        var vertexBufferAcquired = false;

        try
        {
            var image = _imageRegistry.Acquire(texture, colorFormat);
            imageAcquired = true;

            var imageView = _imageViewRegistry.Acquire(imageId, image, format);
            imageViewAcquired = true;

            var sampler = _samplerRegistry.Acquire(samplingBehavior);
            samplerAcquired = true;

            var vertexBuffer = _vertexBufferRegistry.Acquire(renderable);
            vertexBufferAcquired = true;

            var (pipeline, layout, definition) = GetOrCreateTexturedQuadPipeline();
            var descriptorSets = CreateDescriptorSets(
                definition,
                renderable,
                new SampledImage(imageView, sampler)
            );

            return new RenderItem
            {
                Id = id,
                RenderPassMask = RenderPasses.Main,
                Pipelines = CreatePassArray(pipeline, RenderPasses.Main),
                Layouts = CreatePassArray(layout, RenderPasses.Main),
                VertexBuffers = CreatePassArray(vertexBuffer, RenderPasses.Main),
                DescriptorSets = CreatePassArray(descriptorSets.Sets, RenderPasses.Main),
                VertexCount = checked((uint)renderable.Mesh.Count),
            };
        }
        catch
        {
            if (vertexBufferAcquired)
                _vertexBufferRegistry.Release(renderable.Mesh.Id);
            if (samplerAcquired)
                _samplerRegistry.Release(samplerId);
            if (imageViewAcquired)
                _imageViewRegistry.Release(imageViewId);
            if (imageAcquired)
                _imageRegistry.Release(texture.Id);

            throw;
        }
    }

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

        var definition = new PipelineDefinitionBuilder(TEXTURED_QUAD_PIPELINE_NAME, _context)
            .WithShader(vertexShader)
            .WithShader(fragmentShader)
            .WithRenderPass(_swapChain.Passes[RenderPasses.GetIndex(RenderPasses.Main)])
            .WithTopology(vertexShader.Topology)
            .WithDepthTest(false)
            .WithDepthWrite(false)
            .WithCullMode(CullModeFlags.None)
            .WithDescriptorSchema(DescriptorSchemas.Textured)
            .Build();

        var (pipeline, layout) = _pipelineRegistry.GetOrCreate(definition);
        return (pipeline, layout, definition);
    }

    private (DescriptorSet[] Sets, VkBuffer[] UniformBuffers) CreateDescriptorSets(
        PipelineDefinition definition,
        IDrawable renderable,
        SampledImage? sampledImage
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
        var allocatedSets = new List<DescriptorSet>();
        var uniformBuffers = new List<VkBuffer>();

        try
        {
            for (var setIndex = 0; setIndex < schema.Sets.Length; setIndex++)
            {
                var setSchema = schema.Sets[setIndex];
                sets[setIndex] = _descriptorSetPool.Allocate(
                    _pipelineRegistry.GetDescriptorSetLayout(definition.Id, (uint)setIndex)
                );
                allocatedSets.Add(sets[setIndex]);

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
                            uniformBuffers.Add(buffer);
                            break;

                        case DescriptorType.CombinedImageSampler:
                            if (sampledImage is not { } sampled)
                                throw new InvalidOperationException(
                                    $"Pipeline '{definition.Name}' requires a sampled texture."
                                );

                            _descriptorSetPool.WriteCombinedImageSampler(
                                sets[setIndex],
                                binding.Binding,
                                sampled.ImageView,
                                sampled.Sampler
                            );
                            break;
                    }
                }
            }

            if (uniformIndex != uniformContracts.Length)
                throw new InvalidOperationException(
                    $"Pipeline '{definition.Name}' has {uniformContracts.Length} uniform shader contracts but only {uniformIndex} uniform descriptor bindings."
                );

            return (sets, [.. uniformBuffers]);
        }
        catch
        {
            foreach (var set in allocatedSets)
                _descriptorSetPool.Release(set);
            foreach (var buffer in uniformBuffers)
                _bufferManager.DestroyBuffer(buffer);

            throw;
        }
    }

    private void AddRenderItem(RenderItem renderItem)
    {
        EnsureDefaultRenderLayer();
        var layer = _renderer.Layers[0];
        if (!layer.Items.Contains(renderItem))
            layer.Items.Add(renderItem);
    }

    private void OnComponentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IGraphicsComponent component)
            return;

        foreach (var renderable in component.Renderables)
        {
            if (!_renderItemByRenderable.TryGetValue(renderable.Id, out var renderItem))
                continue;

            var renderItemId = GetRenderItemId(renderable);
            if (renderItem.Id == renderItemId)
            {
                renderItem.UpdateInstances(renderable);
                continue;
            }

            Deactivate(renderable);
            Activate(renderable);
        }
    }

    private void Deactivate(IDrawable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);

        if (!_renderItemByRenderable.Remove(renderable.Id, out var renderItem))
            return;

        renderItem.RemoveInstances(renderable.Id);
        if (renderItem.InstanceCount != 0)
            return;

        _renderItems.Remove(renderItem.Id);
        foreach (var layer in _renderer.Layers)
            layer.Items.Remove(renderItem);

        _vertexBufferRegistry.Release(renderable.Mesh.Id);

        if (renderable is TexturedQuadRenderer textured && textured.Texture is { } texture)
        {
            const ColorFormatEnum colorFormat = ColorFormatEnum.RGBA8UNorm;
            var format = colorFormat.ToVulkanFormat();
            var imageId = GetImageResourceId(texture, colorFormat);
            var imageViewId = GetImageViewResourceId(imageId, format);
            var samplerId = GetSamplerResourceId(textured.SamplingBehavior);

            _imageViewRegistry.Release(imageViewId);
            _samplerRegistry.Release(samplerId);
            _imageRegistry.Release(texture.Id);
        }

        _logger.LogDebug(
            "Deactivated renderable. RenderableType={RenderableType}, RenderItemId={RenderItemId}",
            renderable.GetType().Name,
            renderItem.Id
        );
    }

    private static DrawableId GetImageResourceId(ITexture texture, ColorFormatEnum format) =>
        new IdentityHashBuilder(nameof(ImageRegistry))
            .Add(texture.ContentId)
            .Add((ulong)format)
            .Compute();

    private static DrawableId GetImageViewResourceId(DrawableId imageId, Format format) =>
        new IdentityHashBuilder(nameof(ImageViewRegistry))
            .Add(imageId)
            .Add((ulong)format)
            .Compute();

    private static DrawableId GetSamplerResourceId(ISamplingBehavior samplingBehavior) =>
        new IdentityHashBuilder(nameof(SamplerRegistry)).Add(samplingBehavior.Id).Compute();

    private static T[] CreatePassArray<T>(T value, uint passMask)
    {
        var values = new T[RenderPasses.Count];
        values[RenderPasses.GetIndex(passMask)] = value;
        return values;
    }

    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is not IGraphicsComponent component)
            return;

        foreach (var renderable in component.Renderables)
            Activate(renderable);

        component.PropertyChanged += OnComponentPropertyChanged;
    }

    public void Handle(ComponentDeactivatedEvent e)
    {
        if (e.Component is not IGraphicsComponent component)
            return;

        _logger.LogDebug(
            "Graphics component deactivated. ComponentType={ComponentType}, RenderableCount={RenderableCount}",
            component.GetType().Name,
            component.Renderables.Count()
        );

        component.PropertyChanged -= OnComponentPropertyChanged;

        foreach (var renderable in component.Renderables)
            Deactivate(renderable);
    }

    /// <summary>
    /// Creates the default Vulkan render layer covering the full swapchain extent, if one does not already exist.
    /// </summary>
    private void EnsureDefaultRenderLayer()
    {
        if (_renderer.Layers.Any())
            return;

        var extent = _swapChain.SwapchainExtent;

        _logger.LogInformation(
            "Renderer has no render layers; creating the default Vulkan render layer. "
                + "SwapchainExtent={Width}x{Height}, RenderPassName={RenderPassName}",
            extent.Width,
            extent.Height,
            RenderPasses.GetName(RenderPasses.Main)
        );

        var layer = new VulkanRenderLayer
        {
            LoadOp = AttachmentLoadOp.Load,
            Viewport = new()
            {
                X = 0,
                Y = 0,
                Width = extent.Width,
                Height = extent.Height,
                MinDepth = 0f,
                MaxDepth = 1f,
            },
            Scissor = new Rect2D { Offset = new Offset2D(0, 0), Extent = extent },
            RenderPasses =
            [
                new RenderPassDefinition
                {
                    RenderPass = RenderPasses.Main,
                    ClearValues = [new Color(0.02f, 0.02f, 0.02f, 1.0f).ClearValue()],
                    ShouldRender = true,
                },
            ],
            Items = [],
        };

        _renderer.Layers = [layer];

        _logger.LogInformation(
            $"Default Vulkan render layer created. "
                + $"LayerCount={_renderer.Layers.Count()}, "
                + $"RenderPassCount={layer.RenderPasses.Length}, "
                + $"RenderItemCount={layer.Items.Count}"
        );
    }

    /// <summary>
    /// Renders the current frame through the Vulkan renderer.
    /// </summary>
    public void Render()
    {
        try
        {
            _renderer.Render();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vulkan graphics system failed to render a frame.");
            throw;
        }
    }

    /// <summary>
    /// Releases resources owned directly by the graphics system.
    /// </summary>
    /// <param name="disposing">Whether managed resources should also be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            _logger.LogDebug("Vulkan graphics system disposal requested more than once.");
            return;
        }

        _context.VulkanApi.DeviceWaitIdle(_context.Device);

        if (disposing)
        {
            // TODO: dispose managed state (managed objects)
        }

        disposedValue = true;
        _logger.LogInformation("Vulkan graphics system disposed.");
    }

    /// <summary>
    /// Releases resources owned by the graphics system.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
