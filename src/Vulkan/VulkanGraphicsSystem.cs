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
/// <param name="syncManager">The synchronization manager used to establish device-idle shutdown.</param>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    ISyncManager syncManager,
    IEventHub eventHub,
    ICommandFactory commandFactory,
    IVertexBufferRegistry geometryRegistry,
    IInstanceBufferRegistry instanceBufferRegistry,
    IImageRegistry textureRegistry,
    IPipelineRegistry pipelineRegistry
) : IGraphicsSystem, IDisposable
{
    private RenderLayerCollection _layers = new();
    private RenderBatchCollection?[] _batches = new RenderBatchCollection?[
        RenderLayerCollection.MaxLayers
    ];
    private readonly Dictionary<
        DrawableId,
        (
            ulong RenderLayerMask,
            Mesh Mesh,
            ITexture Texture,
            VertexFormat VertexFormat,
            ColorFormatEnum ColorFormat,
            PipelineId PipelineId
        )
    > _drawables = [];
    private readonly Dictionary<PipelineId, int> _pipelineReferences = [];

    private void OnLayerAdded(IRenderLayer layer)
    {
        var coll = new RenderBatchCollection();
        _batches[layer.Index] = coll;

        coll.Set(RenderPasses.Start, new RenderBatch(new DefaultBatchStrategy()));

        foreach (var renderPass in RenderPasses.GetActivePasses(layer.RenderPassMask))
        {
            coll.Set(renderPass, new RenderBatch(new DefaultBatchStrategy()));
        }

        coll.Set(RenderPasses.End, new RenderBatch(new DefaultBatchStrategy()));
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

        Debug.WriteLine(
            $"Vulkan graphics system initialized. DeviceHandle={context.Device.Handle}, "
                + $"SwapchainExtent={swapChain.Extent.Width}x{swapChain.Extent.Height}"
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

        var targetBatches = GetDrawableBatches(drawable.RenderLayerMask).ToArray();
        if (targetBatches.Length == 0)
            return;

        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");
        var colorFormat =
            drawable.FragmentShader?.ColorFormat
            ?? throw new InvalidOperationException("Drawables must define a fragment shader.");
        var commands = commandFactory.Create(drawable).ToArray();

        foreach (var batches in targetBatches)
        {
            foreach (var command in commands)
                AddToBatches(batches, command);
        }

        var pipelineId =
            commands.OfType<BindPipelineCommand>().Single().PipelineId
            ?? throw new InvalidOperationException(
                "Drawable pipeline commands require a pipeline ID."
            );
        _pipelineReferences[pipelineId] = _pipelineReferences.GetValueOrDefault(pipelineId) + 1;
        _drawables[drawable.Id] = (
            drawable.RenderLayerMask,
            drawable.Mesh,
            drawable.Texture,
            vertexShader.VertexFormat,
            colorFormat,
            pipelineId
        );

        drawable.RenderLayerChanged -= OnDrawableChanged;
        drawable.RenderLayerChanged += OnDrawableChanged;
        drawable.MeshChanged -= OnDrawableChanged;
        drawable.MeshChanged += OnDrawableChanged;
        drawable.TextureChanged -= OnDrawableChanged;
        drawable.TextureChanged += OnDrawableChanged;
        drawable.InstanceDataChanged -= OnDrawableChanged;
        drawable.InstanceDataChanged += OnDrawableChanged;
        drawable.UniformDataChanged -= OnDrawableChanged;
        drawable.UniformDataChanged += OnDrawableChanged;
        drawable.ShaderChanged -= OnDrawableChanged;
        drawable.ShaderChanged += OnDrawableChanged;

        Debug.WriteLine(
            $"Activated drawable. DrawableType={drawable.GetType().Name}, DrawableId={drawable.Id}"
        );
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

        component.DrawableAdded += OnDrawableAdded;
        component.DrawableRemoved += OnDrawableRemoved;

        foreach (var drawable in component.Drawables)
            ActivateDrawable(drawable);
    }

    /// <summary>Activates a drawable added to an active graphics component.</summary>
    /// <param name="sender">The graphics component that raised the event.</param>
    /// <param name="e">The added drawable.</param>
    private void OnDrawableAdded(object? sender, DrawableEventArgs e)
    {
        ActivateDrawable(e.Drawable);
    }

    /// <summary>Releases a drawable removed from an active graphics component.</summary>
    /// <param name="sender">The graphics component that raised the event.</param>
    /// <param name="e">The removed drawable.</param>
    private void OnDrawableRemoved(object? sender, DrawableEventArgs e)
    {
        Deactivate(e.Drawable);
    }

    /// <summary>
    /// Recreates the Vulkan resources and commands associated with a changed drawable.
    /// </summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableChanged(object? sender, EventArgs e)
    {
        if (sender is not IDrawable drawable || !_drawables.ContainsKey(drawable.Id))
            return;

        Deactivate(drawable);
        ActivateDrawable(drawable);
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

        if (!_drawables.Remove(drawable.Id, out var registration))
            return;

        drawable.RenderLayerChanged -= OnDrawableChanged;
        drawable.MeshChanged -= OnDrawableChanged;
        drawable.TextureChanged -= OnDrawableChanged;
        drawable.InstanceDataChanged -= OnDrawableChanged;
        drawable.UniformDataChanged -= OnDrawableChanged;
        drawable.ShaderChanged -= OnDrawableChanged;

        var targetBatches = GetDrawableBatches(registration.RenderLayerMask).ToArray();
        foreach (var batches in targetBatches)
        {
            foreach (var renderPass in RenderPasses.GetActivePasses(RenderPasses.All))
            {
                if (batches.TryGet(renderPass, out var batch))
                    batch!.Remove(drawable.Id);
            }
        }

        if (_pipelineReferences.TryGetValue(registration.PipelineId, out var referenceCount))
        {
            if (referenceCount == 1)
            {
                _pipelineReferences.Remove(registration.PipelineId);
                pipelineRegistry.Release(registration.PipelineId);
            }
            else
            {
                _pipelineReferences[registration.PipelineId] = referenceCount - 1;
            }
        }
        geometryRegistry.Release(registration.Mesh, registration.VertexFormat);
        instanceBufferRegistry.Release(drawable.Id);

        var releaseCommands = textureRegistry.Release(
            registration.Texture,
            registration.ColorFormat
        );
        if (targetBatches.Length > 0)
        {
            foreach (var command in releaseCommands)
                AddToBatches(targetBatches[0], command);
        }

        Debug.WriteLine(
            $"Deactivated drawable. DrawableType={drawable.GetType().Name}, DrawableId={drawable.Id}"
        );
    }

    /// <summary>
    /// Gets the batch collections selected by a drawable's layer mask.
    /// </summary>
    /// <param name="renderLayerMask">The mask of render-layer slots to include.</param>
    /// <returns>The active batch collections selected by the mask.</returns>
    private IEnumerable<RenderBatchCollection> GetDrawableBatches(ulong renderLayerMask)
    {
        if (_batches.Length == 0)
            yield break;

        if (renderLayerMask == 0)
        {
            if (_batches[0] is { } defaultBatches)
                yield return defaultBatches;

            yield break;
        }

        foreach (var layer in _layers.Get(renderLayerMask))
        {
            if (_batches[layer.Index] is { } batches)
                yield return batches;
        }
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

            Debug.WriteLine("Cleared Vulkan graphics view configuration.");
            return;
        }

        if (e.Component is not IGraphicsComponent component)
            return;

        component.DrawableAdded -= OnDrawableAdded;
        component.DrawableRemoved -= OnDrawableRemoved;

        Debug.WriteLine(
            $"Graphics component deactivated. ComponentType={component.GetType().Name}, DrawableCount={component.Drawables.Count()}"
        );

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
            Debug.WriteLine("Vulkan graphics system disposal requested more than once.");
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
        Debug.WriteLine("Vulkan graphics system disposed.");
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
