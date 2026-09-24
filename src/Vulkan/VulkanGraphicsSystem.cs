namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
/// <param name="context">The Vulkan context that owns graphics resources.</param>
/// <param name="swapChain">The swap chain used for presentation.</param>
/// <param name="renderer">The renderer used to record and submit render batches.</param>
/// <param name="geometryRegistry">The registry that manages vertex buffers.</param>
/// <param name="textureRegistry">The registry that manages images.</param>
/// <param name="renderPassConfig">The shared render-pass configurations.</param>
/// <param name="eventHub">The event hub used to register this graphics system.</param>
/// <param name="logger">The logger used to record graphics system activity.</param>
/// <param name="syncManager">The synchronization manager used to establish device-idle shutdown.</param>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    ISyncManager syncManager,
    IEventHub eventHub,
    ICommandFactory commandFactory,
    IVertexBufferRegistry geometryRegistry,
    IImageRegistry textureRegistry,
    IPipelineRegistry pipelineRegistry,
    ILogger<VulkanGraphicsSystem> logger
) : IGraphicsSystem, IDisposable
{
    private RenderLayerCollection _layers = new();
    private RenderBatchCollection?[] _batches = new RenderBatchCollection?[
        RenderLayerCollection.MaxLayers
    ];

    private void OnLayerAdded(IRenderLayer layer)
    {
        var coll = new RenderBatchCollection();
        _batches[layer.Index] = coll;

        coll.Set(RenderPasses.Start, new RenderBatch(new DefaultBatchStrategy(), logger));

        foreach (var renderPass in RenderPasses.GetActivePasses(layer.RenderPassMask))
        {
            coll.Set(renderPass, new RenderBatch(new DefaultBatchStrategy(), logger));
        }

        coll.Set(RenderPasses.End, new RenderBatch(new DefaultBatchStrategy(), logger));
    }

    private void OnLayerRemoved(IRenderLayer layer)
    {
        _batches[layer.Index] = null;
    }

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        eventHub.Register(this);

        _layers.LayerAdded += OnLayerAdded;
        _layers.LayerRemoved += OnLayerRemoved;

        logger.LogInformation(
            "Vulkan graphics system initialized. DeviceHandle={DeviceHandle}, "
                + "SwapchainExtent={Width}x{Height}",
            context.Device.Handle,
            swapChain.Extent.Width,
            swapChain.Extent.Height
        );
    }

    /// <summary>
    /// Creates render batches for the specified view configuration.
    /// </summary>
    /// <param name="view">The activated view configuration.</param>
    private void ActivateViewComponent(ViewComponent view)
    {
        var layer = _layers.Create(view.Name, view.RenderPassMask);
        view.RenderLayer = layer;
    }

    private void ActivateDrawable(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (_batches.Length == 0)
            return;

        var batches = _batches[0];
        if (batches is null)
            return;

        foreach (var command in commandFactory.Create(drawable))
            AddToBatches(batches, command);

        logger.LogTrace(
            "Activated drawable. DrawableType={DrawableType}, DrawableId={DrawableId}",
            drawable.GetType().Name,
            drawable.Id
        );
    }

    /// <summary>
    /// Reconstructs the pipeline definition used by a drawable.
    /// </summary>
    /// <param name="drawable">The drawable whose shader state defines the pipeline.</param>
    /// <param name="renderPass">The render pass used by the drawable.</param>
    /// <param name="vertexShader">The drawable's required vertex shader.</param>
    /// <returns>The pipeline definition for the drawable.</returns>
    private PipelineDefinition CreatePipelineDefinition(
        IDrawable drawable,
        uint renderPass,
        VertexShader vertexShader
    )
    {
        var pipelineDefinitionBuilder = new PipelineDefinitionBuilder(
            drawable.GetType().Name,
            context
        )
            .WithShader(vertexShader)
            .WithRenderPass(swapChain.Passes[RenderPasses.GetIndex(renderPass)]);

        if (drawable.TessellationControlShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationControlShader);
        if (drawable.TessellationEvalShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationEvalShader);
        if (drawable.GeometryShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.GeometryShader);
        if (drawable.FragmentShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.FragmentShader);

        return pipelineDefinitionBuilder.Build();
    }

    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is ViewComponent view)
        {
            ActivateViewComponent(view);
            return;
        }

        if (e.Component is not IGraphicsComponent component)
            return;

        foreach (var drawable in component.Drawables)
            ActivateDrawable(drawable);

        component.PropertyChanged += OnDrawablePropertyChanged;
    }

    private void OnDrawablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // TODO: handle updates
    }

    /// <summary>
    /// Adds a command to each batch selected by its render-pass mask.
    /// </summary>
    /// <param name="batches">The render batches that own the command.</param>
    /// <param name="command">The command to allocate to its execution phase.</param>
    private static void AddToBatches(RenderBatchCollection batches, IVulkanCommand command)
    {
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(command);

        if (command.RenderPassMask is RenderPasses.Start or RenderPasses.End)
        {
            if (batches.TryGet(command.RenderPassMask, out var phaseBatch))
                phaseBatch!.Add(command);

            return;
        }

        foreach (var renderPass in RenderPasses.GetActivePasses(command.RenderPassMask))
        {
            if (batches.TryGet(renderPass, out var batch))
                batch!.Add(command);
        }
    }

    private void Deactivate(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (_batches.Length == 0)
            return;

        var batches = _batches[0];
        if (batches is null)
            return;

        foreach (var renderPass in RenderPasses.GetActivePasses(RenderPasses.All))
        {
            if (batches.TryGet(renderPass, out var batch))
                batch!.Remove(drawable.Id);
        }

        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");
        var colorFormat =
            drawable.FragmentShader?.ColorFormat
            ?? throw new InvalidOperationException("Drawables must define a fragment shader.");
        var pipelineDefinition = CreatePipelineDefinition(
            drawable,
            RenderPasses.Main,
            vertexShader
        );

        pipelineRegistry.Release(pipelineDefinition.Id);
        geometryRegistry.Release(drawable.Mesh, vertexShader.VertexFormat);
        geometryRegistry.ReleaseInstanceBuffer(drawable.Id);

        foreach (var command in textureRegistry.Release(drawable.Texture, colorFormat))
            AddToBatches(batches, command);

        logger.LogTrace(
            "Deactivated drawable. DrawableType={DrawableType}, DrawableId={DrawableId}",
            drawable.GetType().Name,
            drawable.Id
        );
    }

    public void Handle(ComponentDeactivatedEvent e)
    {
        if (e.Component is ViewComponent view)
        {
            if (view.RenderLayer is { } layer)
            {
                _layers.Remove(layer.Index);
                view.RenderLayer = null;
            }

            logger.LogDebug("Cleared Vulkan graphics view configuration.");
            return;
        }

        if (e.Component is not IGraphicsComponent component)
            return;

        logger.LogDebug(
            "Graphics component deactivated. ComponentType={ComponentType}, DrawableCount={DrawableCount}",
            component.GetType().Name,
            component.Drawables.Count()
        );

        component.PropertyChanged -= OnDrawablePropertyChanged;

        foreach (var drawable in component.Drawables)
            Deactivate(drawable);
    }

    /// <summary>
    /// Renders the current frame through the Vulkan renderer.
    /// </summary>
    public void Render()
    {
        try
        {
            var frameSync = syncManager.WaitForFrame(syncManager.CurrentFrameIndex);

            if (renderer.PrepareFrame(frameSync) is null)
                return;

            for (var layerIndex = 0; layerIndex < _batches.Length; layerIndex++)
            {
                var batches = _batches[layerIndex];
                if (batches is null)
                    continue;

                renderer.Begin(batches.Get(RenderPasses.Start));

                foreach (var renderPass in RenderPasses.GetActivePasses(RenderPasses.All))
                {
                    if (!batches.TryGet(renderPass, out var batch))
                        continue;

                    renderer.Record(RenderPasses.GetIndex(renderPass), batch!);
                }

                renderer.Finalize(batches.Get(RenderPasses.End));
            }

            renderer.Submit();
            CleanTransientCommands();
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// Removes commands that have been successfully submitted and are not retained across frames.
    /// </summary>
    private void CleanTransientCommands()
    {
        foreach (var batches in _batches)
        {
            if (batches is null)
                continue;

            foreach (var batch in batches)
                batch.Clean();
        }
    }

    private bool disposedValue;

    /// <summary>
    /// Releases resources owned directly by the graphics system.
    /// </summary>
    /// <param name="disposing">Whether managed resources should also be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            logger.LogDebug("Vulkan graphics system disposal requested more than once.");
            return;
        }

        syncManager.DeviceWaitIdle();
        geometryRegistry.Reset();
        textureRegistry.Reset();

        if (disposing)
        {
            // TODO: dispose managed state (managed objects)
        }

        disposedValue = true;
        logger.LogInformation("Vulkan graphics system disposed.");
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
