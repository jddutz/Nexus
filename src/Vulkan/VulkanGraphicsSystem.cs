namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
/// <param name="context">The Vulkan context that owns graphics resources.</param>
/// <param name="swapChain">The swap chain used for presentation.</param>
/// <param name="renderer">The renderer used to record and submit render batches.</param>
/// <param name="geometryRegistry">The registry reset when the graphics system shuts down.</param>
/// <param name="textureRegistry">The image registry reset when the graphics system shuts down.</param>
/// <param name="eventHub">The event hub used to register this graphics system.</param>
/// <param name="syncManager">The synchronization manager used to establish device-idle shutdown.</param>
/// <param name="commandFactory">The owner of per-drawable Vulkan allocations and commands.</param>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    ISyncManager syncManager,
    IEventHub eventHub,
    ICommandFactory commandFactory,
    IVertexBufferRegistry geometryRegistry,
    IImageRegistry textureRegistry
) : IGraphicsSystem, IDisposable
{
    private RenderLayerCollection _layers = new();
    private RenderBatchCollection?[] _batches = new RenderBatchCollection?[
        RenderLayerCollection.MaxLayers
    ];
    private readonly Dictionary<DrawableId, DrawableRegistration> _drawables = [];
    private readonly HashSet<IGraphicsComponent> _components = [];

    /// <summary>Creates render batches for a newly added layer.</summary>
    /// <param name="layer">The layer being added.</param>
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

    /// <summary>Removes render batches for a layer being removed.</summary>
    /// <param name="layer">The layer being removed.</param>
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

    /// <summary>Creates and registers commands for an active drawable.</summary>
    /// <param name="drawable">The drawable to activate.</param>
    private void ActivateDrawable(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        var targetBatches = GetDrawableBatches(drawable.RenderLayerMask).ToArray();
        if (targetBatches.Length == 0)
            return;

        var commands = commandFactory.Create(drawable).ToArray();
        var persistentCommands = commands.Where(command => command.IsSticky).ToArray();
        var transientCommands = commands.Where(command => !command.IsSticky).ToArray();

        foreach (var batches in targetBatches)
            foreach (var command in persistentCommands)
                AddToBatches(batches, command);

        foreach (var command in transientCommands)
            AddToBatches(targetBatches[0], command);

        _drawables[drawable.Id] = new(
            drawable,
            drawable.RenderLayerMask,
            persistentCommands,
            transientCommands
        );

        drawable.RenderLayerChanged += OnDrawableRenderLayerChanged;
        drawable.MeshChanged += OnDrawableMeshChanged;
        drawable.TextureChanged += OnDrawableTextureChanged;
        drawable.InstanceDataChanged += OnDrawableInstanceDataChanged;
        drawable.UniformDataChanged += OnDrawableUniformDataChanged;
        drawable.ShaderChanged += OnDrawableShaderChanged;

        Debug.WriteLine(
            $"Activated drawable. DrawableType={drawable.GetType().Name}, DrawableId={drawable.Id}"
        );
    }

    /// <inheritdoc />
    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is ViewComponent view)
        {
            ActivateViewComponent(view);
            return;
        }

        if (e.Component is not IGraphicsComponent component)
            return;

        if (_components.Add(component))
        {
            component.DrawableAdded += OnDrawableAdded;
            component.DrawableRemoved += OnDrawableRemoved;
        }

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

    /// <summary>Moves a drawable's existing commands when its render-layer membership changes.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableRenderLayerChanged(object? sender, EventArgs e)
    {
        if (sender is not IDrawable drawable || !_drawables.ContainsKey(drawable.Id))
            return;

        var registration = _drawables[drawable.Id];
        if (registration.RenderLayerMask == drawable.RenderLayerMask)
            return;

        foreach (var batches in GetDrawableBatches(registration.RenderLayerMask))
        {
            foreach (var batch in batches)
                batch.Remove(drawable.Id);
            foreach (var command in registration.TransientCommands)
                foreach (var batch in batches)
                    batch.RemoveCommand(command.Id);
        }

        var targetBatches = GetDrawableBatches(drawable.RenderLayerMask).ToArray();
        foreach (var batches in targetBatches)
            foreach (var command in registration.Commands)
                AddToBatches(batches, command);
        if (targetBatches.Length > 0)
            foreach (var command in registration.TransientCommands)
                AddToBatches(targetBatches[0], command);

        _drawables[drawable.Id] = registration with
        {
            RenderLayerMask = drawable.RenderLayerMask,
        };
    }

    /// <summary>Updates instance buffers and draw counts for a changed drawable.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableInstanceDataChanged(object? sender, EventArgs e) =>
        UpdateDrawable(sender, commandFactory.UpdateInstanceData);

    /// <summary>Updates uniform buffers for a changed drawable.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableUniformDataChanged(object? sender, EventArgs e) =>
        UpdateDrawable(sender, commandFactory.UpdateUniformData);

    /// <summary>Updates image and sampler descriptors for a changed drawable.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableTextureChanged(object? sender, EventArgs e) =>
        UpdateDrawable(sender, commandFactory.UpdateTexture);

    /// <summary>Updates vertex-buffer bindings for a changed drawable.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableMeshChanged(object? sender, EventArgs e) =>
        UpdateDrawable(sender, commandFactory.UpdateMesh);

    /// <summary>Updates pipelines and dependent commands for a changed drawable.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableShaderChanged(object? sender, EventArgs e) =>
        UpdateDrawable(sender, commandFactory.UpdateShaders, replaceAllCommands: true);

    /// <summary>Applies the factory update for an active drawable.</summary>
    /// <param name="sender">The object that raised the drawable event.</param>
    /// <param name="update">The targeted factory operation.</param>
    /// <param name="replaceAllCommands">Whether the operation replaces the complete command set.</param>
    private void UpdateDrawable(
        object? sender,
        Func<IDrawable, IEnumerable<IVulkanCommand>> update,
        bool replaceAllCommands = false
    )
    {
        if (sender is not IDrawable drawable || !_drawables.ContainsKey(drawable.Id))
            return;

        ApplyDrawableCommands(drawable, update(drawable).ToArray(), replaceAllCommands);
    }

    /// <summary>Replaces only affected command slots and queues transient commands.</summary>
    /// <param name="drawable">The drawable whose commands changed.</param>
    /// <param name="commands">The new or transient commands from the factory.</param>
    /// <param name="replaceAllCommands">Whether to remove every retained command first.</param>
    private void ApplyDrawableCommands(
        IDrawable drawable,
        IVulkanCommand[] commands,
        bool replaceAllCommands
    )
    {
        var registration = _drawables[drawable.Id];
        var retainedCommands = registration.Commands.ToList();
        var transientCommands = registration.TransientCommands.ToList();
        var clearOldCommands = replaceAllCommands;
        foreach (var command in commands)
        {
            if (!command.IsSticky || command.Drawable?.Id != drawable.Id)
            {
                var targetBatches = GetDrawableBatches(drawable.RenderLayerMask).ToArray();
                if (!command.IsSticky)
                {
                    transientCommands.Add(command);
                    if (targetBatches.Length > 0)
                        AddToBatches(targetBatches[0], command);
                }
                else
                {
                    foreach (var batches in targetBatches)
                        AddToBatches(batches, command);
                }

                continue;
            }

            var replacedCommands = clearOldCommands
                ? retainedCommands.ToArray()
                : retainedCommands.Where(existing => SameCommandSlot(existing, command)).ToArray();
            clearOldCommands = false;

            foreach (var replaced in replacedCommands)
            {
                foreach (var batches in GetDrawableBatches(registration.RenderLayerMask))
                    foreach (var batch in batches)
                        batch.RemoveCommand(replaced.Id);

                retainedCommands.Remove(replaced);
            }

            foreach (var batches in GetDrawableBatches(drawable.RenderLayerMask))
                AddToBatches(batches, command);

            retainedCommands.Add(command);
        }

        _drawables[drawable.Id] = registration with
        {
            RenderLayerMask = drawable.RenderLayerMask,
            Commands = retainedCommands.ToArray(),
            TransientCommands = transientCommands.ToArray(),
        };
    }

    /// <summary>Determines whether two commands occupy the same drawable command slot.</summary>
    /// <param name="left">The current command.</param>
    /// <param name="right">The replacement command.</param>
    /// <returns><see langword="true"/> when the replacement supersedes the current command.</returns>
    private static bool SameCommandSlot(IVulkanCommand left, IVulkanCommand right)
    {
        if (left.GetType() != right.GetType())
            return false;

        return left is not BindVertexBufferCommand leftBinding
            || right is not BindVertexBufferCommand rightBinding
            || leftBinding.Binding == rightBinding.Binding;
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

    /// <summary>Releases an active drawable and removes its commands from batches.</summary>
    /// <param name="drawable">The drawable to deactivate.</param>
    private void Deactivate(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (!_drawables.Remove(drawable.Id, out var registration))
            return;

        drawable.RenderLayerChanged -= OnDrawableRenderLayerChanged;
        drawable.MeshChanged -= OnDrawableMeshChanged;
        drawable.TextureChanged -= OnDrawableTextureChanged;
        drawable.InstanceDataChanged -= OnDrawableInstanceDataChanged;
        drawable.UniformDataChanged -= OnDrawableUniformDataChanged;
        drawable.ShaderChanged -= OnDrawableShaderChanged;

        var targetBatches = GetDrawableBatches(registration.RenderLayerMask).ToArray();
        foreach (var batches in targetBatches)
        {
            foreach (var renderPass in RenderPasses.GetActivePasses(RenderPasses.All))
            {
                if (batches.TryGet(renderPass, out var batch))
                    batch!.Remove(drawable.Id);
            }

            foreach (var command in registration.TransientCommands)
                foreach (var batch in batches)
                    batch.RemoveCommand(command.Id);
        }

        var releaseCommands = commandFactory.Release(drawable).ToArray();
        if (targetBatches.Length > 0)
        {
            foreach (var command in releaseCommands)
                AddToBatches(targetBatches[0], command);
        }

        Debug.WriteLine(
            $"Deactivated drawable. DrawableType={drawable.GetType().Name}, DrawableId={drawable.Id}"
        );
    }

    /// <summary>Retains the drawable's batch placement and current command sets.</summary>
    /// <param name="Drawable">The active drawable.</param>
    /// <param name="RenderLayerMask">The layer placement for the retained commands.</param>
    /// <param name="Commands">Persistent commands currently registered in batches.</param>
    /// <param name="TransientCommands">One-shot commands awaiting submission.</param>
    private sealed record DrawableRegistration(
        IDrawable Drawable,
        ulong RenderLayerMask,
        IVulkanCommand[] Commands,
        IVulkanCommand[] TransientCommands
    );

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

    /// <inheritdoc />
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

        if (_components.Remove(component))
        {
            component.DrawableAdded -= OnDrawableAdded;
            component.DrawableRemoved -= OnDrawableRemoved;
        }

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

        foreach (var drawableId in _drawables.Keys.ToArray())
            _drawables[drawableId] = _drawables[drawableId] with { TransientCommands = [] };
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

        foreach (var component in _components)
        {
            component.DrawableAdded -= OnDrawableAdded;
            component.DrawableRemoved -= OnDrawableRemoved;
        }
        _components.Clear();

        foreach (var drawable in _drawables.Values.Select(registration => registration.Drawable).ToArray())
            Deactivate(drawable);

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
