namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, view batch preparation, and frame rendering.
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
    IImageRegistry textureRegistry,
    PerformanceMetrics? performanceMetrics = null
) : IGraphicsSystem, IDisposable
{
    private readonly PerformanceMetrics? _performanceMetrics = performanceMetrics;
    private readonly RenderBatchCollection _setupBatches = CreateSetupBatchCollection();
    private readonly Dictionary<ComponentId, RenderBatchCollection> _batches = [];
    private readonly List<ViewComponent> _activeViews = [];
    private readonly Dictionary<DrawableId, DrawableRegistration> _drawables = [];
    private readonly HashSet<IGraphicsComponent> _components = [];

    /// <summary>Creates the render batches enabled by a view.</summary>
    /// <param name="view">The view whose render passes configure the collection.</param>
    private static RenderBatchCollection CreateRenderBatchCollection(ViewComponent view)
    {
        var coll = new RenderBatchCollection();
        coll.Set(RenderPasses.Start, new RenderBatch(new DefaultBatchStrategy()));

        foreach (var renderPass in RenderPasses.GetActivePasses(view.RenderPassMask))
        {
            coll.Set(renderPass, new RenderBatch(new DefaultBatchStrategy()));
        }

        coll.Set(RenderPasses.End, new RenderBatch(new DefaultBatchStrategy()));
        return coll;
    }

    /// <summary>Creates the shared batch collection for frame setup commands.</summary>
    /// <returns>A collection containing the setup phases and every render-pass slot.</returns>
    private static RenderBatchCollection CreateSetupBatchCollection()
    {
        var coll = new RenderBatchCollection();
        coll.Set(RenderPasses.Start, new RenderBatch(new DefaultBatchStrategy()));

        foreach (var renderPass in RenderPasses.GetActivePasses(RenderPasses.All))
            coll.Set(renderPass, new RenderBatch(new DefaultBatchStrategy()));

        coll.Set(RenderPasses.End, new RenderBatch(new DefaultBatchStrategy()));
        return coll;
    }

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        eventHub.Register(this);

        Debug.WriteLine(
            $"Vulkan graphics system initialized. DeviceHandle={context.Device.Handle}, "
                + $"SwapchainExtent={swapChain.Extent.Width}x{swapChain.Extent.Height}"
        );
    }

    /// <summary>Creates and populates the batch collection for an active view.</summary>
    /// <param name="view">The activated view configuration.</param>
    private void ActivateViewComponent(ViewComponent view)
    {
        if (_batches.ContainsKey(view.Id))
            throw new InvalidOperationException($"View {view.Id} is already active.");

        var batches = CreateRenderBatchCollection(view);
        _activeViews.Add(view);
        _batches.Add(view.Id, batches);
        view.PropertyChanged += OnViewPropertyChanged;

        foreach (var registration in _drawables.Values)
        {
            if (IsVisible(view, registration.Drawable))
                AddDrawableToBatches(batches, registration);
        }
    }

    /// <summary>Creates and registers commands for an active drawable.</summary>
    /// <param name="drawable">The drawable to activate.</param>
    private void ActivateDrawable(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        var commands = commandFactory.Create(drawable).ToArray();
        var persistentCommands = commands.Where(command => command.IsSticky).ToArray();
        var transientCommands = commands.Where(command => !command.IsSticky).ToArray();

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

        foreach (var batches in GetDrawableBatches(drawable.RenderLayerMask))
        foreach (var command in persistentCommands)
            AddToBatches(batches, command);

        foreach (var command in transientCommands)
            AddToBatches(_setupBatches, command);

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

    /// <summary>Applies view mutations to membership or pass configuration.</summary>
    /// <param name="sender">The view that changed.</param>
    /// <param name="e">The changed property name.</param>
    private void OnViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ViewComponent view || !_batches.ContainsKey(view.Id))
            return;

        switch (e.PropertyName)
        {
            case nameof(ViewComponent.LayerMask):
                RebuildViewBatches(view, replaceCollection: false);
                break;
            case nameof(ViewComponent.RenderPassMask):
                RebuildViewBatches(view, replaceCollection: true);
                break;
        }
    }

    /// <summary>Rebuilds a view collection or its drawable membership.</summary>
    /// <param name="view">The view whose collection is updated.</param>
    /// <param name="replaceCollection">Whether to recreate pass batches.</param>
    private void RebuildViewBatches(ViewComponent view, bool replaceCollection)
    {
        var batches = replaceCollection ? CreateRenderBatchCollection(view) : _batches[view.Id];

        if (replaceCollection)
            _batches[view.Id] = batches;
        else
        {
            foreach (var registration in _drawables.Values)
                RemoveDrawableFromBatches(batches, registration);
        }

        foreach (var registration in _drawables.Values)
        {
            if (IsVisible(view, registration.Drawable))
                AddDrawableToBatches(batches, registration);
        }
    }

    /// <summary>Adds retained drawable commands to a view collection.</summary>
    /// <param name="batches">The target view collection.</param>
    /// <param name="registration">The drawable command registration.</param>
    private static void AddDrawableToBatches(
        RenderBatchCollection batches,
        DrawableRegistration registration
    )
    {
        foreach (var command in registration.Commands)
            AddToBatches(batches, command);
    }

    /// <summary>Removes a drawable's retained and pending commands from one view collection.</summary>
    /// <param name="batches">The view collection to clear.</param>
    /// <param name="registration">The drawable command registration.</param>
    private static void RemoveDrawableFromBatches(
        RenderBatchCollection batches,
        DrawableRegistration registration
    )
    {
        foreach (var batch in batches)
        {
            batch.Remove(registration.Drawable.Id);
            foreach (var command in registration.Commands.Concat(registration.TransientCommands))
                batch.RemoveCommand(command.Id);
        }
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

    /// <summary>Moves a drawable's commands when its view membership changes.</summary>
    /// <param name="sender">The drawable that changed.</param>
    /// <param name="e">The event data.</param>
    private void OnDrawableRenderLayerChanged(object? sender, EventArgs e)
    {
        if (sender is not IDrawable drawable || !_drawables.ContainsKey(drawable.Id))
            return;

        var registration = _drawables[drawable.Id];
        if (registration.RenderLayerMask == drawable.RenderLayerMask)
            return;

        RemoveDrawableFromViewBatches(registration);
        registration = registration with { RenderLayerMask = drawable.RenderLayerMask };
        _drawables[drawable.Id] = registration;

        var targetBatches = GetDrawableBatches(drawable.RenderLayerMask);
        foreach (var batches in targetBatches)
        foreach (var command in registration.Commands)
            AddToBatches(batches, command);
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
                    AddToBatches(_setupBatches, command);
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
                RemoveCommandFromAllViewBatches(replaced.Id);

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

        RemoveDrawableFromViewBatches(registration);
        RemoveDrawableFromBatches(_setupBatches, registration);

        var releaseCommands = commandFactory.Release(drawable).ToArray();
        foreach (var command in releaseCommands)
            AddToBatches(_setupBatches, command);

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
    /// Gets the batch collections for views selected by a drawable's layer mask.
    /// </summary>
    /// <param name="renderLayerMask">The drawable's layer classification mask.</param>
    /// <returns>The active view batch collections selected by the mask.</returns>
    private IEnumerable<RenderBatchCollection> GetDrawableBatches(ulong renderLayerMask)
    {
        return _activeViews
            .Where(view => (view.LayerMask & renderLayerMask) != 0)
            .Select(view => _batches[view.Id]);
    }

    /// <summary>Removes a drawable's commands from every active view collection.</summary>
    /// <param name="registration">The drawable command registration.</param>
    private void RemoveDrawableFromViewBatches(DrawableRegistration registration)
    {
        foreach (var batches in _batches.Values)
            RemoveDrawableFromBatches(batches, registration);
    }

    /// <summary>Removes a command identifier from every active view collection.</summary>
    /// <param name="commandId">The command identifier to remove.</param>
    private void RemoveCommandFromAllViewBatches(Guid commandId)
    {
        foreach (var batches in _batches.Values)
        foreach (var batch in batches)
            batch.RemoveCommand(commandId);
    }

    /// <summary>Determines whether a drawable's layer classification is selected by a view.</summary>
    /// <param name="view">The view being evaluated.</param>
    /// <param name="drawable">The drawable being evaluated.</param>
    /// <returns><see langword="true"/> when the masks intersect.</returns>
    private static bool IsVisible(ViewComponent view, IDrawable drawable) =>
        (view.LayerMask & drawable.RenderLayerMask) != 0;

    /// <summary>Gets the view's clipped pixel region, defaulting an unset region to the full target.</summary>
    /// <param name="view">The view whose clipping region is converted.</param>
    /// <returns>The target-clamped pixel offset and extent.</returns>
    private (int X, int Y, uint Width, uint Height) GetClippingRegion(ViewComponent view)
    {
        var targetWidth = checked((int)swapChain.Extent.Width);
        var targetHeight = checked((int)swapChain.Extent.Height);
        var region = view.ClippingRegion;

        if (region.Origin.X == 0 && region.Origin.Y == 0 && region.Max.X == 0 && region.Max.Y == 0)
            return (0, 0, checked((uint)targetWidth), checked((uint)targetHeight));

        var x = Math.Clamp(region.Origin.X, 0, targetWidth);
        var y = Math.Clamp(region.Origin.Y, 0, targetHeight);
        var right = Math.Clamp(region.Max.X, x, targetWidth);
        var bottom = Math.Clamp(region.Max.Y, y, targetHeight);
        return (x, y, checked((uint)(right - x)), checked((uint)(bottom - y)));
    }

    /// <inheritdoc />
    public void Handle(ComponentDeactivatedEvent e)
    {
        if (e.Component is ViewComponent view)
        {
            if (_batches.Remove(view.Id))
            {
                view.PropertyChanged -= OnViewPropertyChanged;
                _activeViews.Remove(view);
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
        var frameStartTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var frameSync = syncManager.WaitForFrame(syncManager.CurrentFrameIndex);

            if (renderer.PrepareFrame(frameSync) is null)
                return;

            renderer.Begin(_setupBatches.Get(RenderPasses.Start));

            foreach (var renderPass in RenderPasses.GetActivePasses(RenderPasses.All))
            {
                if (
                    !_setupBatches.TryGet(renderPass, out var setupBatch)
                    || !setupBatch!.Commands.Any()
                )
                    continue;

                renderer.Record(RenderPasses.GetIndex(renderPass), setupBatch);
            }

            renderer.Finalize(_setupBatches.Get(RenderPasses.End));

            foreach (
                var view in _activeViews
                    .OrderBy(item => item.RenderOrder)
                    .ThenBy(item => item.Id.Value)
            )
            {
                var clip = GetClippingRegion(view);
                if (clip.Width == 0 || clip.Height == 0)
                    continue;

                view.Camera?.SetViewportSize(clip.Width, clip.Height);
                var viewProjectionMatrix =
                    view.Camera?.ViewProjectionMatrix ?? Matrix4X4<float>.Identity;
                var batches = _batches[view.Id];

                AddToBatches(
                    batches,
                    new SetViewportScissorCommand(
                        RenderPasses.Start,
                        clip.X,
                        clip.Y,
                        clip.Width,
                        clip.Height
                    )
                );

                foreach (var registration in _drawables.Values)
                {
                    if (!IsVisible(view, registration.Drawable))
                        continue;

                    foreach (
                        var command in commandFactory.CreateViewProjectionCommands(
                            registration.Drawable,
                            viewProjectionMatrix
                        )
                    )
                        AddToBatches(batches, command);
                }

                AddToBatches(
                    batches,
                    new SetViewportScissorCommand(
                        RenderPasses.End,
                        0,
                        0,
                        swapChain.Extent.Width,
                        swapChain.Extent.Height
                    )
                );

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
            _performanceMetrics?.RecordFrame(Stopwatch.GetElapsedTime(frameStartTimestamp));
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
        foreach (var batch in _setupBatches)
            batch.Clean();

        foreach (var batches in _batches.Values)
        {
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

        foreach (var view in _activeViews)
            view.PropertyChanged -= OnViewPropertyChanged;
        _activeViews.Clear();

        foreach (
            var drawable in _drawables
                .Values.Select(registration => registration.Drawable)
                .ToArray()
        )
            Deactivate(drawable);

        geometryRegistry.Reset();
        textureRegistry.Reset();
        _performanceMetrics?.Output();

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
