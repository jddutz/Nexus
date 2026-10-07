namespace Nexus.Graphics.Vulkan;

using Nexus.Core.Performance;

/// <summary>
/// Coordinates Vulkan resource loading, view batch preparation, and frame rendering.
/// </summary>
/// <param name="context">The Vulkan context that owns graphics resources.</param>
/// <param name="swapChain">The swap chain used for presentation.</param>
/// <param name="renderer">The renderer used to record and submit render batches.</param>
/// <param name="instanceBufferRegistry">The registry reset when the graphics system shuts down.</param>
/// <param name="geometryRegistry">The registry reset when the graphics system shuts down.</param>
/// <param name="textureRegistry">The image registry reset when the graphics system shuts down.</param>
/// <param name="eventHub">The event hub used to register this graphics system.</param>
/// <param name="syncManager">The synchronization manager used to establish device-idle shutdown.</param>
/// <param name="commandFactory">The owner of per-drawable Vulkan allocations and commands.</param>
/// <param name="performanceMetrics">Optional performance counter collector.</param>
/// <param name="diagnostics">Optional immutable Vulkan diagnostic collector.</param>
/// <param name="telemetry">The optional performance telemetry sink.</param>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    ICommandRecorder renderer,
    ISyncManager syncManager,
    IEventHub eventHub,
    ICommandFactory commandFactory,
    IInstanceBufferRegistry instanceBufferRegistry,
    IVertexBufferRegistry geometryRegistry,
    IImageRegistry textureRegistry,
    PerformanceMetrics? performanceMetrics = null,
    PerformanceDiagnostics? diagnostics = null,
    IPerformanceTelemetry? telemetry = null
) : IGraphicsSystem, IDisposable
{
    private readonly PerformanceMetrics? _performanceMetrics = performanceMetrics;
    private readonly PerformanceDiagnostics? _diagnostics = diagnostics;
    private readonly RenderBatchCollection _setupBatches = CreateSetupBatchCollection();
    private readonly Dictionary<ComponentId, RenderBatchCollection> _batches = [];
    private readonly List<ViewRenderer> _activeViews = [];
    private readonly Dictionary<DrawableId, DrawableRegistration> _drawables = [];
    private readonly Dictionary<DrawableId, Action<string>> _drawablePropertyChangedHandlers = [];
    private readonly Dictionary<IRenderer, Action<bool, bool>> _rendererVisibilityChangedHandlers =
        [];
    private readonly HashSet<IRenderer> _components = [];

    /// <summary>Creates the render batches enabled by a view.</summary>
    /// <param name="view">The view whose render passes configure the collection.</param>
    private static RenderBatchCollection CreateRenderBatchCollection(ViewRenderer view)
    {
        var coll = new RenderBatchCollection();
        IBatchStrategy batchStrategy = view.PreserveDrawOrder
            ? new DrawOrderBatchStrategy()
            : new DefaultBatchStrategy();
        coll.Set(RenderPasses.Start, new RenderBatch(new DefaultBatchStrategy()));

        foreach (var renderPass in RenderPasses.GetActivePasses(view.RenderPassMask))
        {
            coll.Set(renderPass, new RenderBatch(batchStrategy));
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

    /// <summary>Gets active views selected by a drawable's layer mask.</summary>
    /// <param name="renderLayerMask">The drawable's layer classification mask.</param>
    /// <returns>The matching active views.</returns>
    private IEnumerable<ViewRenderer> GetDrawableViews(ulong renderLayerMask) =>
        _activeViews.Where(view => (view.LayerMask & renderLayerMask) != 0);

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        using var timing = new LoadPerformanceScope(
            telemetry,
            "startup.system.initialize",
            "graphics"
        );
        eventHub.Register(this);

        Debug.WriteLine(
            $"Vulkan graphics system initialized. DeviceHandle={context.Device.Handle}, "
                + $"SwapchainExtent={swapChain.Extent.Width}x{swapChain.Extent.Height}"
                + $"{_diagnostics?.FrameCountLogSuffix}"
        );
    }

    /// <summary>Creates and populates the batch collection for an active view.</summary>
    /// <param name="view">The activated view configuration.</param>
    private void ActivateViewComponent(ViewRenderer view)
    {
        if (_batches.ContainsKey(view.Id))
            throw new InvalidOperationException($"View {view.Id} is already active.");

        var batches = CreateRenderBatchCollection(view);
        _activeViews.Add(view);
        _batches.Add(view.Id, batches);
        view.PropertyChanged += OnViewPropertyChanged;

        Debug.WriteLine(
            $"View activated. ComponentId={view.Id}, LayerMask={view.LayerMask}, RenderPassMask={view.RenderPassMask}, ClippingRegion={view.ClippingRegion}, CameraType={view.Camera?.GetType().Name}{_diagnostics?.FrameCountLogSuffix}"
        );

        foreach (var registration in _drawables.Values)
        {
            if (IsVisible(view, registration.Drawable))
            {
                AddDrawableToBatches(view, batches, registration);
            }
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
            transientCommands,
            []
        );

        Action<string> propertyChanged = propertyName =>
            OnDrawablePropertyChanged(drawable, propertyName);
        _drawablePropertyChangedHandlers.Add(drawable.Id, propertyChanged);
        drawable.PropertyChanged += propertyChanged;
        drawable.InstanceDataChanged += OnDrawableInstanceDataChanged;
        drawable.UniformDataChanged += OnDrawableUniformDataChanged;

        var registration = _drawables[drawable.Id];
        foreach (var view in GetDrawableViews(drawable.RenderLayerMask))
        {
            var viewCommands = commandFactory.CreateViewCommands(drawable, view).ToArray();
            registration.ViewCommands[view.Id] = viewCommands;
            foreach (var command in viewCommands)
                AddToBatches(_batches[view.Id], command);
        }

        foreach (var command in transientCommands)
            AddToBatches(_setupBatches, command);

        Debug.WriteLine(
            $"Drawable activated. DrawableId={drawable.Id}, DrawableType={drawable.GetType().Name}, RenderLayerMask={drawable.RenderLayerMask}, InstanceCount={drawable.InstanceCount}{_diagnostics?.FrameCountLogSuffix}"
        );
    }

    /// <inheritdoc />
    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is ViewRenderer view)
        {
            ActivateViewComponent(view);
            return;
        }

        if (e.Component is not IRenderer component)
            return;

        if (_components.Add(component))
        {
            component.DrawableAdded += OnDrawableAdded;
            component.DrawableRemoved += OnDrawableRemoved;
            Action<bool, bool> visibilityChanged = (_, isVisible) =>
                OnRendererVisibilityChanged(component, isVisible);
            _rendererVisibilityChangedHandlers.Add(component, visibilityChanged);
            component.IsVisibleChanged += visibilityChanged;
        }

        if (component.IsVisible)
        {
            foreach (var drawable in component.Drawables)
                ActivateDrawable(drawable);
        }
    }

    /// <summary>Applies view mutations to membership or pass configuration.</summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnViewPropertyChanged(string propertyName)
    {
        foreach (var view in _activeViews)
        {
            if (!_batches.ContainsKey(view.Id))
                continue;

            switch (propertyName)
            {
                case nameof(ViewRenderer.LayerMask):
                    RebuildViewBatches(view, replaceCollection: false);
                    break;
                case nameof(ViewRenderer.RenderPassMask):
                    RebuildViewBatches(view, replaceCollection: true);
                    break;
                case nameof(ViewRenderer.PreserveDrawOrder):
                case nameof(ViewRenderer.BlendMode):
                case nameof(ViewRenderer.EnableDepthTest):
                case nameof(ViewRenderer.EnableDepthWrite):
                case nameof(ViewRenderer.DepthComparison):
                    RebuildViewBatches(view, replaceCollection: true);
                    break;
            }
        }
    }

    /// <summary>Rebuilds a view collection or its drawable membership.</summary>
    /// <param name="view">The view whose collection is updated.</param>
    /// <param name="replaceCollection">Whether to recreate pass batches.</param>
    private void RebuildViewBatches(ViewRenderer view, bool replaceCollection)
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
                AddDrawableToBatches(view, batches, registration);
        }
    }

    /// <summary>Adds retained drawable commands to a view collection.</summary>
    /// <param name="batches">The target view collection.</param>
    /// <param name="registration">The drawable command registration.</param>
    private void AddDrawableToBatches(
        ViewRenderer view,
        RenderBatchCollection batches,
        DrawableRegistration registration
    )
    {
        var commands = commandFactory.CreateViewCommands(registration.Drawable, view).ToArray();
        registration.ViewCommands[view.Id] = commands;
        foreach (var command in commands)
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
            foreach (
                var command in registration
                    .Commands.Concat(registration.TransientCommands)
                    .Concat(registration.ViewCommands.Values.SelectMany(commands => commands))
            )
                batch.RemoveCommand(command.Id);
        }
    }

    /// <summary>Activates a drawable added to an active graphics component.</summary>
    /// <param name="sender">The graphics component that raised the event.</param>
    /// <param name="e">The added drawable.</param>
    private void OnDrawableAdded(object? sender, DrawableEventArgs e)
    {
        if (sender is IRenderer { IsVisible: true })
            ActivateDrawable(e.Drawable);
    }

    /// <summary>Updates drawable registrations when a renderer's visibility changes.</summary>
    /// <param name="component">The renderer whose visibility changed.</param>
    /// <param name="isVisible">Whether the renderer should submit its drawables.</param>
    private void OnRendererVisibilityChanged(IRenderer component, bool isVisible)
    {
        foreach (var drawable in component.Drawables)
        {
            if (isVisible)
                ActivateDrawable(drawable);
            else
                Deactivate(drawable);
        }
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
    private void OnDrawableRenderLayerMaskChanged(object? sender, EventArgs e)
    {
        if (sender is not IDrawable drawable || !_drawables.ContainsKey(drawable.Id))
            return;

        var registration = _drawables[drawable.Id];
        if (registration.RenderLayerMask == drawable.RenderLayerMask)
            return;

        RemoveDrawableFromViewBatches(registration);
        registration = registration with { RenderLayerMask = drawable.RenderLayerMask };
        _drawables[drawable.Id] = registration;

        foreach (var view in GetDrawableViews(drawable.RenderLayerMask))
            AddDrawableToBatches(view, _batches[view.Id], registration);
    }

    /// <summary>Routes observable drawable properties to their Vulkan update operation.</summary>
    /// <param name="drawable">The drawable whose property changed.</param>
    /// <param name="propertyName">The changed property name.</param>
    private void OnDrawablePropertyChanged(IDrawable drawable, string propertyName)
    {
        switch (propertyName)
        {
            case nameof(IDrawable.RenderLayerMask):
                OnDrawableRenderLayerMaskChanged(drawable, EventArgs.Empty);
                break;
            case nameof(IDrawable.DrawOrder):
                UpdateDrawable(drawable, commandFactory.UpdateDrawOrder);
                break;
            case nameof(IDrawable.Mesh):
                UpdateDrawable(drawable, commandFactory.UpdateMesh, replaceAllCommands: true);
                break;
            case nameof(IDrawable.Texture):
            case nameof(IDrawable.SamplingBehavior):
                UpdateDrawable(drawable, commandFactory.UpdateTexture);
                break;
            case nameof(IDrawable.VertexShader):
            case nameof(IDrawable.TessellationControlShader):
            case nameof(IDrawable.TessellationEvalShader):
            case nameof(IDrawable.GeometryShader):
            case nameof(IDrawable.FragmentShader):
                UpdateDrawable(drawable, commandFactory.UpdateShaders, replaceAllCommands: true);
                break;
        }
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
                if (!command.IsSticky)
                {
                    transientCommands.Add(command);
                    AddToBatches(_setupBatches, command);
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

            retainedCommands.Add(command);
        }

        registration = registration with
        {
            RenderLayerMask = drawable.RenderLayerMask,
            Commands = retainedCommands.ToArray(),
            TransientCommands = transientCommands.ToArray(),
        };
        _drawables[drawable.Id] = registration;
        RefreshDrawableViewCommands(registration);
    }

    /// <summary>Recreates a drawable's per-view commands after its retained state changes.</summary>
    /// <param name="registration">The drawable registration to refresh.</param>
    private void RefreshDrawableViewCommands(DrawableRegistration registration)
    {
        foreach (var view in _activeViews)
        {
            var batches = _batches[view.Id];
            if (registration.ViewCommands.TryGetValue(view.Id, out var oldCommands))
            {
                foreach (var batch in batches)
                foreach (var command in oldCommands)
                    batch.RemoveCommand(command.Id);
            }

            if (!IsVisible(view, registration.Drawable))
            {
                registration.ViewCommands.Remove(view.Id);
                continue;
            }

            var commands = commandFactory.CreateViewCommands(registration.Drawable, view).ToArray();
            registration.ViewCommands[view.Id] = commands;
            foreach (var command in commands)
                AddToBatches(batches, command);
        }
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

        if (_drawablePropertyChangedHandlers.Remove(drawable.Id, out var propertyChanged))
            drawable.PropertyChanged -= propertyChanged;
        drawable.InstanceDataChanged -= OnDrawableInstanceDataChanged;
        drawable.UniformDataChanged -= OnDrawableUniformDataChanged;

        RemoveDrawableFromViewBatches(registration);
        RemoveDrawableFromBatches(_setupBatches, registration);

        var releaseCommands = commandFactory.Release(drawable).ToArray();
        foreach (var command in releaseCommands)
            AddToBatches(_setupBatches, command);

        Debug.WriteLine(
            $"Deactivated drawable. DrawableType={drawable.GetType().Name}, DrawableId={drawable.Id}{_diagnostics?.FrameCountLogSuffix}"
        );
    }

    /// <summary>Retains the drawable's batch placement and current command sets.</summary>
    /// <param name="Drawable">The active drawable.</param>
    /// <param name="RenderLayerMask">The layer placement for the retained commands.</param>
    /// <param name="Commands">Persistent commands currently registered in batches.</param>
    /// <param name="TransientCommands">One-shot commands awaiting submission.</param>
    /// <param name="ViewCommands">Persistent commands configured for each active view.</param>
    private sealed record DrawableRegistration(
        IDrawable Drawable,
        ulong RenderLayerMask,
        IVulkanCommand[] Commands,
        IVulkanCommand[] TransientCommands,
        Dictionary<ComponentId, IVulkanCommand[]> ViewCommands
    );

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
    private static bool IsVisible(ViewRenderer view, IDrawable drawable) =>
        (view.LayerMask & drawable.RenderLayerMask) != 0;

    /// <summary>Gets the view's clipped pixel region, defaulting an unset region to the full target.</summary>
    /// <param name="view">The view whose clipping region is converted.</param>
    /// <returns>The target-clamped pixel offset and extent.</returns>
    private (int X, int Y, uint Width, uint Height) GetClippingRegion(ViewRenderer view)
    {
        var targetWidth = checked((int)swapChain.Extent.Width);
        var targetHeight = checked((int)swapChain.Extent.Height);
        var region = view.ClippingRegion;

        if (
            !view.HasExplicitClippingRegion
            && region.Origin.X == 0
            && region.Origin.Y == 0
            && region.Max.X == 0
            && region.Max.Y == 0
        )
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
        if (e.Component is ViewRenderer view)
        {
            if (_batches.Remove(view.Id))
            {
                view.PropertyChanged -= OnViewPropertyChanged;
                _activeViews.Remove(view);
                foreach (var registration in _drawables.Values)
                    registration.ViewCommands.Remove(view.Id);
            }

            Debug.WriteLine(
                $"Cleared Vulkan graphics view configuration.{_diagnostics?.FrameCountLogSuffix}"
            );
            return;
        }

        if (e.Component is not IRenderer component)
            return;

        if (_components.Remove(component))
        {
            component.DrawableAdded -= OnDrawableAdded;
            component.DrawableRemoved -= OnDrawableRemoved;
            if (
                _rendererVisibilityChangedHandlers.Remove(
                    component,
                    out var visibilityChanged
                )
            )
                component.IsVisibleChanged -= visibilityChanged;
        }

        Debug.WriteLine(
            $"Graphics component deactivated. ComponentType={component.GetType().Name}, DrawableCount={component.Drawables.Count()}{_diagnostics?.FrameCountLogSuffix}"
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
                var viewCamera = view.Camera;
                var selectedDrawables = _drawables.Values
                    .Where(registration => IsVisible(view, registration.Drawable))
                    .ToArray();

                if (clip.Width == 0 || clip.Height == 0 || viewCamera is null)
                {
                    RecordViewDiagnostics(
                        view,
                        clip,
                        viewCamera,
                        selectedDrawables.Length,
                        0,
                        0,
                        skipped: true
                    );
                    continue;
                }

                var batches = _batches[view.Id];
                var viewProjectionMatrix = viewCamera.ViewProjectionMatrix;

                AddToBatches(
                    batches,
                    new SetViewportScissorCommand(
                        RenderPasses.Start,
                        view.ViewportRegion,
                        view.ClippingRegion
                    )
                );

                foreach (var registration in selectedDrawables)
                {
                    var viewProjectionCommands = commandFactory
                        .CreateViewProjectionCommands(registration.Drawable, viewProjectionMatrix)
                        .ToArray();

                    foreach (var command in viewProjectionCommands)
                        AddToBatches(batches, command);
                }

                AddToBatches(
                    batches,
                    new SetViewportScissorCommand(
                        RenderPasses.End,
                        new Rectangle<int>(
                            0,
                            0,
                            checked((int)swapChain.Extent.Width),
                            checked((int)swapChain.Extent.Height)
                        ),
                        new Rectangle<int>(
                            0,
                            0,
                            checked((int)swapChain.Extent.Width),
                            checked((int)swapChain.Extent.Height)
                        )
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
                RecordViewDiagnostics(
                    view,
                    clip,
                    viewCamera,
                    selectedDrawables.Length,
                    batches.Count(batch => batch.Commands.Any()),
                    batches.Sum(batch => batch.Commands.Count()),
                    skipped: false
                );
            }

            renderer.Submit();
            _diagnostics?.RecordFrameSubmitted();
            _performanceMetrics?.RecordFrame(Stopwatch.GetElapsedTime(frameStartTimestamp));
            CleanTransientCommands();
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// Records one view-level state and batch summary without emitting per-draw diagnostics.
    /// </summary>
    /// <param name="view">The view whose state was evaluated.</param>
    /// <param name="clip">The effective target-clamped clipping region.</param>
    /// <param name="camera">The camera selected by the view.</param>
    /// <param name="selectedDrawableCount">The number of layer-selected drawables.</param>
    /// <param name="preparedBatchCount">The number of non-empty prepared batches.</param>
    /// <param name="preparedCommandCount">The number of commands in prepared batches.</param>
    /// <param name="skipped">Whether rendering was skipped for this view.</param>
    private void RecordViewDiagnostics(
        ViewRenderer view,
        (int X, int Y, uint Width, uint Height) clip,
        Nexus.Graphics.Cameras.ICamera? camera,
        int selectedDrawableCount,
        int preparedBatchCount,
        int preparedCommandCount,
        bool skipped
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        var values = new Dictionary<string, string>
        {
            ["ViewComponentId"] = view.Id.ToString(),
            ["CameraComponentId"] = camera is Nexus.Core.IComponent cameraComponent
                ? cameraComponent.Id.ToString()
                : "n/a",
            ["LayerMask"] = $"0x{view.LayerMask:X}",
            ["RenderPassMask"] = view.RenderPassMask.ToString(),
            ["RenderOrder"] = view.RenderOrder.ToString(),
            ["ClippingRegion"] = view.ClippingRegion.ToString() ?? "n/a",
            ["EffectiveClippingRegion"] = $"{clip.X},{clip.Y} {clip.Width}x{clip.Height}",
            ["CameraType"] = camera?.GetType().Name ?? "n/a",
            ["SelectedDrawableCount"] = selectedDrawableCount.ToString(),
            ["PreparedBatchCount"] = preparedBatchCount.ToString(),
            ["PreparedCommandCount"] = preparedCommandCount.ToString(),
            ["RenderingSkipped"] = skipped.ToString(),
        };

        var matrixBytes = new byte[64];
        if (camera is not null)
        {
            var viewProjectionMatrix = camera.ViewProjectionMatrix;
            MemoryMarshal.Write(matrixBytes.AsSpan(), in viewProjectionMatrix);
            values["ViewProjectionMatrix"] = viewProjectionMatrix.ToString();
        }
        else
        {
            values["ViewProjectionMatrix"] = "n/a";
        }

        _diagnostics.Record(
            new PerformanceDiagnosticSnapshot(
                default,
                "View",
                view.Id.ToString(),
                values,
                matrixBytes
            )
        );
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
            Debug.WriteLine(
                $"Vulkan graphics system disposal requested more than once.{_diagnostics?.FrameCountLogSuffix}"
            );
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

        instanceBufferRegistry.Reset();
        geometryRegistry.Reset();
        textureRegistry.Reset();
        _performanceMetrics?.Output();
        _diagnostics?.Output();

        if (disposing)
        {
            // TODO: dispose managed state (managed objects)
        }

        disposedValue = true;
        Debug.WriteLine($"Vulkan graphics system disposed.{_diagnostics?.FrameCountLogSuffix}");
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
