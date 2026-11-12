namespace Nexus.GUI.Elements;

using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;

/// <summary>
/// Defines a rendering viewport whose screen area is determined by layout.
/// Selects scene content through a camera and render-layer mask.
/// </summary>
public partial class View : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];
    private IEventHub? _inputEventHub;
    private IInputSystem? _inputSystem;
    private Func<Vector2D<float>, bool>? _pointerHitTest;
    private bool _inputSubscriptionsRegistered;
    private readonly List<MouseMovedEvent> _hoveringMice = [];
    private readonly Dictionary<
        (ITouchScreenInputDevice TouchScreen, int TouchId),
        Vector2D<float>
    > _insideTouches = [];
    private readonly Dictionary<
        (IMouseInputDevice Mouse, MouseButtonEnum Button),
        (Ray3D<float> StartRay, Vector2D<float> Position)
    > _mouseInteractions = [];
    private readonly Dictionary<
        (ITouchScreenInputDevice TouchScreen, int TouchId),
        (Ray3D<float> StartRay, Vector2D<float> Position)
    > _touchInteractions = [];

    /// <summary>Gets or sets the camera used to project selected scene content.</summary>
    [Observable]
    private ICamera? _camera;

    /// <summary>Gets or sets the scene render layers visible through this view.</summary>
    [Observable]
    private ulong _layerMask = RenderLayers.All;

    /// <summary>Gets or sets the rendering order relative to other views.</summary>
    [Observable]
    private int _renderOrder;

    /// <summary>Gets or sets whether drawable ordering is preserved within this view.</summary>
    [Observable]
    private bool _preserveDrawOrder;

    /// <summary>Gets or sets the blending mode used when rendering this view.</summary>
    [Observable]
    private Nexus.Graphics.BlendMode _blendMode = Nexus.Graphics.BlendMode.Alpha;

    /// <summary>Gets or sets how the camera projection is mapped into the arranged bounds.</summary>
    [Observable(PublicSetter = true)]
    private ViewSizingMode _sizingMode = ViewSizingMode.Fit;

    /// <summary>Gets or sets whether this view forwards pointer input as world-ray events.</summary>
    [Observable(PublicSetter = true)]
    private bool _propagateInputEvents;

    /// <summary>Gets the renderer owned by this view.</summary>
    public ViewRenderer ViewComponent { get; }

    /// <summary>Initializes a view with its single owned renderer.</summary>
    public View()
        : base([new ViewRenderer()])
    {
        ViewComponent = Components.OfType<ViewRenderer>().Single();
        ViewComponent.HasExplicitClippingRegion = true;
        ViewComponent.Camera = Camera;
        ViewComponent.LayerMask = LayerMask;
        ViewComponent.RenderOrder = RenderOrder;
        ViewComponent.PreserveDrawOrder = PreserveDrawOrder;
        ViewComponent.BlendMode = BlendMode;
        ViewComponent.ViewportRegion = ViewComponent.ClippingRegion;
    }

    /// <summary>Projects a world point into absolute screen pixels using the rendered viewport.</summary>
    /// <param name="worldPoint">The world-space point to project.</param>
    /// <param name="screenPoint">The projected point, including the viewport origin.</param>
    /// <returns>Whether the view and homogeneous projection are valid.</returns>
    public bool TryProjectToScreen(Vector3D<float> worldPoint, out Vector2D<float> screenPoint)
    {
        screenPoint = default;
        var viewport = ViewComponent.ViewportRegion;
        if (!IsEffectivelyVisible || Camera is null || viewport.Size.X <= 0 || viewport.Size.Y <= 0)
            return false;

        var clip = Vector4D.Transform(
            new Vector4D<float>(worldPoint, 1f),
            Camera.ViewProjectionMatrix
        );
        if (!float.IsFinite(clip.W) || clip.W <= 0f)
            return false;

        // Match rendering: Vulkan NDC has +Y down, including orthographic world views.
        var x = viewport.Origin.X + (clip.X / clip.W + 1f) * 0.5f * viewport.Size.X;
        var y = viewport.Origin.Y + (clip.Y / clip.W + 1f) * 0.5f * viewport.Size.Y;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(clip.Z))
            return false;
        screenPoint = new(x, y);
        return true;
    }

    /// <summary>Synchronizes the assigned camera with the owned renderer.</summary>
    /// <param name="previousValue">The camera previously assigned to the view.</param>
    protected virtual partial void AfterCameraChanges(ICamera? previousValue)
    {
        ViewComponent.Camera = Camera;
        UpdateViewport();
    }

    /// <summary>Synchronizes the layer mask with the owned renderer.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterLayerMaskChanges(ulong previousValue) =>
        ViewComponent.LayerMask = LayerMask;

    /// <summary>Synchronizes the rendering order with the owned renderer.</summary>
    /// <param name="previousValue">The previous view rendering order.</param>
    protected virtual partial void AfterRenderOrderChanges(int previousValue) =>
        ViewComponent.RenderOrder = RenderOrder;

    /// <summary>Synchronizes draw-order preservation with the owned renderer.</summary>
    /// <param name="previousValue">The previous draw-order preservation setting.</param>
    protected virtual partial void AfterPreserveDrawOrderChanges(bool previousValue) =>
        ViewComponent.PreserveDrawOrder = PreserveDrawOrder;

    /// <summary>Synchronizes the blending mode with the owned renderer.</summary>
    /// <param name="previousValue">The previous blending mode.</param>
    protected virtual partial void AfterBlendModeChanges(Nexus.Graphics.BlendMode previousValue) =>
        ViewComponent.BlendMode = BlendMode;

    /// <summary>Updates the viewport when the sizing mode changes.</summary>
    /// <param name="previousValue">The sizing mode before the change.</param>
    protected virtual partial void AfterSizingModeChanges(ViewSizingMode previousValue) =>
        UpdateViewport();

    /// <summary>Updates input subscriptions when propagation is toggled.</summary>
    /// <param name="previousValue">The previous propagation setting.</param>
    protected virtual partial void AfterPropagateInputEventsChanges(bool previousValue) =>
        UpdateInputSubscriptions();

    /// <summary>Updates the clipping rectangle when the arranged element bounds change.</summary>
    /// <param name="previousValue">The bounds before the layout change.</param>
    protected override void AfterBoundsChanges(Rectangle<float> previousValue)
    {
        base.AfterBoundsChanges(previousValue);
        UpdateViewport();
    }

    /// <summary>Updates the clipping rectangle when this element's visibility changes.</summary>
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        UpdateViewport();
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        base.Arrange(bounds);
        UpdateViewport();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateViewport();
    }

    /// <summary>Associates this active view with the services used for direct pointer input.</summary>
    /// <param name="inputSystem">The input system providing immediate pointer events, if available.</param>
    /// <param name="eventHub">The event hub that dispatches view-ray events.</param>
    /// <param name="pointerHitTest">Determines whether the GUI permits this view to receive a pointer event.</param>
    internal void RegisterInput(
        IInputSystem? inputSystem,
        IEventHub eventHub,
        Func<Vector2D<float>, bool> pointerHitTest
    )
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(pointerHitTest);
        if (_inputEventHub is not null)
            throw new InvalidOperationException("The view is already registered for input.");

        _inputEventHub = eventHub;
        _inputSystem = inputSystem;
        _pointerHitTest = pointerHitTest;
        UpdateInputSubscriptions();
    }

    /// <summary>Removes this view's direct pointer input subscriptions.</summary>
    internal void UnregisterInput()
    {
        ExitAllPointers();
        CancelAllInteractions();
        if (_inputSubscriptionsRegistered)
        {
            if (_inputSystem is not null)
            {
                _inputSystem.MouseMoved -= OnMouseMoved;
                _inputSystem.MouseButtonPressed -= OnMouseButtonPressed;
                _inputSystem.MouseButtonReleased -= OnMouseButtonReleased;
                _inputSystem.MouseWheel -= OnMouseWheel;
                _inputSystem.MouseCanceled -= OnMouseCanceled;
                _inputSystem.MouseDisconnected -= OnMouseDisconnected;
                _inputSystem.TouchStarted -= OnTouchStarted;
                _inputSystem.TouchMoved -= OnTouchMoved;
                _inputSystem.TouchEnded -= OnTouchEnded;
            }

            _inputSubscriptionsRegistered = false;
        }

        _inputSystem = null;
        _inputEventHub = null;
        _pointerHitTest = null;
    }

    /// <summary>Synchronizes input subscriptions with the propagation setting.</summary>
    private void UpdateInputSubscriptions()
    {
        if (_inputEventHub is null || PropagateInputEvents == _inputSubscriptionsRegistered)
            return;

        if (PropagateInputEvents)
        {
            if (_inputSystem is not null)
            {
                _inputSystem.MouseMoved += OnMouseMoved;
                _inputSystem.MouseButtonPressed += OnMouseButtonPressed;
                _inputSystem.MouseButtonReleased += OnMouseButtonReleased;
                _inputSystem.MouseWheel += OnMouseWheel;
                _inputSystem.MouseCanceled += OnMouseCanceled;
                _inputSystem.MouseDisconnected += OnMouseDisconnected;
                _inputSystem.TouchStarted += OnTouchStarted;
                _inputSystem.TouchMoved += OnTouchMoved;
                _inputSystem.TouchEnded += OnTouchEnded;
            }

            _inputSubscriptionsRegistered = true;
            return;
        }

        ExitAllPointers();
        CancelAllInteractions();
        if (_inputSystem is not null)
        {
            _inputSystem.MouseMoved -= OnMouseMoved;
            _inputSystem.MouseButtonPressed -= OnMouseButtonPressed;
            _inputSystem.MouseButtonReleased -= OnMouseButtonReleased;
            _inputSystem.MouseWheel -= OnMouseWheel;
            _inputSystem.MouseCanceled -= OnMouseCanceled;
            _inputSystem.MouseDisconnected -= OnMouseDisconnected;
            _inputSystem.TouchStarted -= OnTouchStarted;
            _inputSystem.TouchMoved -= OnTouchMoved;
            _inputSystem.TouchEnded -= OnTouchEnded;
        }

        _inputSubscriptionsRegistered = false;
    }

    /// <summary>Called when a mouse enters this view's interactive region.</summary>
    /// <param name="mouse">The mouse that entered the region.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    /// <param name="worldRay">The camera ray through the pointer position.</param>
    protected virtual void OnMouseEntered(
        IMouseInputDevice mouse,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a mouse leaves this view's interactive region.</summary>
    /// <param name="mouse">The mouse that left the region.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    protected virtual void OnMouseExited(IMouseInputDevice mouse, Vector2D<float> position) { }

    /// <summary>Called when mouse movement is published from this view.</summary>
    /// <param name="mouse">The mouse that moved.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    /// <param name="worldRay">The camera ray through the pointer position.</param>
    protected virtual void OnMouseMoved(
        IMouseInputDevice mouse,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a mouse-button press is published from this view.</summary>
    /// <param name="mouse">The mouse that reported the press.</param>
    /// <param name="button">The pressed button.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    /// <param name="worldRay">The camera ray through the pointer position.</param>
    protected virtual void OnMouseButtonPressed(
        IMouseInputDevice mouse,
        MouseButtonEnum button,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a mouse-button release is published from this view.</summary>
    /// <param name="mouse">The mouse that reported the release.</param>
    /// <param name="button">The released button.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    /// <param name="worldRay">The camera ray through the pointer position.</param>
    protected virtual void OnMouseButtonReleased(
        IMouseInputDevice mouse,
        MouseButtonEnum button,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when mouse-wheel movement is published from this view.</summary>
    /// <param name="mouse">The mouse that reported the wheel movement.</param>
    /// <param name="delta">The scroll delta.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    /// <param name="worldRay">The camera ray through the pointer position.</param>
    protected virtual void OnMouseWheel(
        IMouseInputDevice mouse,
        Vector2D<float> delta,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a mouse interaction cancellation is published from this view.</summary>
    /// <param name="mouse">The mouse whose interaction was canceled.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    /// <param name="worldRay">The camera ray through the pointer position or retained press ray.</param>
    protected virtual void OnMouseCanceled(
        IMouseInputDevice mouse,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a touch start is published from this view.</summary>
    /// <param name="touchScreen">The touch device that reported the start.</param>
    /// <param name="touchId">The device-local identifier of the touch.</param>
    /// <param name="position">The absolute screen-space touch position.</param>
    /// <param name="worldRay">The camera ray through the touch position.</param>
    protected virtual void OnTouchStarted(
        ITouchScreenInputDevice touchScreen,
        int touchId,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when an owned touch leaves this view's interactive region.</summary>
    /// <param name="touchScreen">The touch device that owns the touch.</param>
    /// <param name="touchId">The device-local identifier of the touch.</param>
    /// <param name="position">The absolute screen-space touch position.</param>
    protected virtual void OnTouchExited(
        ITouchScreenInputDevice touchScreen,
        int touchId,
        Vector2D<float> position
    ) { }

    /// <summary>Called when touch movement is published from this view.</summary>
    /// <param name="touchScreen">The touch device that owns the touch.</param>
    /// <param name="touchId">The device-local identifier of the touch.</param>
    /// <param name="position">The absolute screen-space touch position.</param>
    /// <param name="worldRay">The camera ray through the touch position.</param>
    protected virtual void OnTouchMoved(
        ITouchScreenInputDevice touchScreen,
        int touchId,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a touch end is published from this view.</summary>
    /// <param name="touchScreen">The touch device that owns the touch.</param>
    /// <param name="touchId">The device-local identifier of the touch.</param>
    /// <param name="position">The absolute screen-space touch position.</param>
    /// <param name="worldRay">The camera ray through the touch position or retained start ray.</param>
    protected virtual void OnTouchEnded(
        ITouchScreenInputDevice touchScreen,
        int touchId,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Called when a touch interaction cancellation is published from this view.</summary>
    /// <param name="touchScreen">The touch device that owns the touch.</param>
    /// <param name="touchId">The device-local identifier of the touch.</param>
    /// <param name="position">The last absolute screen-space touch position.</param>
    /// <param name="worldRay">The camera ray through the last position or retained start ray.</param>
    protected virtual void OnTouchCanceled(
        ITouchScreenInputDevice touchScreen,
        int touchId,
        Vector2D<float> position,
        Ray3D<float> worldRay
    ) { }

    /// <summary>Reports hover transitions and movement inside the view or during a captured drag.</summary>
    /// <param name="message">The source mouse movement.</param>
    private void OnMouseMoved(MouseMovedEvent message)
    {
        UpdateMouseInteractionPositions(message.Mouse, message.Position);
        var inside = TryCreateWorldRay(message.Position, out var ray);
        UpdateMouseRegion(message, inside, ray);
        if (inside)
        {
            OnMouseMoved(message.Mouse, message.Position, ray);
            PublishInputEvent(new ViewMouseMovedEvent(message, ray));
            return;
        }

        if (
            IsInputActive
            && _mouseInteractions.Keys.Any(key => ReferenceEquals(key.Mouse, message.Mouse))
            && TryProjectWorldRay(message.Position, out ray)
        )
        {
            OnMouseMoved(message.Mouse, message.Position, ray);
            PublishInputEvent(new ViewMouseMovedEvent(message, ray));
        }
    }

    /// <summary>Updates one mouse's region membership without changing its captured interactions.</summary>
    /// <param name="message">The current mouse position.</param>
    /// <param name="inside">Whether the GUI-aware interactive region contains the pointer.</param>
    /// <param name="ray">The projected ray when the pointer is inside.</param>
    private void UpdateMouseRegion(MouseMovedEvent message, bool inside, Ray3D<float> ray)
    {
        var previousIndex = _hoveringMice.FindIndex(item =>
            ReferenceEquals(item.Mouse, message.Mouse)
        );
        if (inside)
        {
            if (previousIndex < 0)
            {
                _hoveringMice.Add(message);
                OnMouseEntered(message.Mouse, message.Position, ray);
                PublishInputEvent(new ViewMouseEnteredEvent(message, ray));
            }
            else
            {
                _hoveringMice[previousIndex] = message;
            }

            return;
        }

        if (previousIndex >= 0)
        {
            _hoveringMice.RemoveAt(previousIndex);
            OnMouseExited(message.Mouse, message.Position);
            PublishInputEvent(new ViewMouseExitedEvent(message));
        }
    }

    /// <summary>Publishes a world-ray event for a mouse button press inside this view.</summary>
    /// <param name="message">The source mouse button press.</param>
    private void OnMouseButtonPressed(MouseButtonPressedEvent message)
    {
        UpdateMouseInteractionPositions(message.Mouse, message.Position);
        if (TryCreateWorldRay(message.Position, out var ray))
        {
            UpdateMouseRegion(new MouseMovedEvent(message.Mouse, message.Position), true, ray);
            _mouseInteractions[(message.Mouse, message.Button)] = (ray, message.Position);
            OnMouseButtonPressed(message.Mouse, message.Button, message.Position, ray);
            PublishInputEvent(new ViewMouseButtonPressedEvent(message, ray));
        }
    }

    /// <summary>Publishes a release inside this view or terminates a mouse interaction it accepted.</summary>
    /// <param name="message">The source mouse button release.</param>
    private void OnMouseButtonReleased(MouseButtonReleasedEvent message)
    {
        UpdateMouseInteractionPositions(message.Mouse, message.Position);
        if (!TryCreateWorldRay(message.Position, out _))
            UpdateMouseRegion(new MouseMovedEvent(message.Mouse, message.Position), false, default);

        if (_mouseInteractions.Remove((message.Mouse, message.Button), out var interaction))
        {
            var releaseRay = TryProjectWorldRay(message.Position, out var projectedRay)
                ? projectedRay
                : interaction.StartRay;
            OnMouseButtonReleased(message.Mouse, message.Button, message.Position, releaseRay);
            PublishInputEvent(new ViewMouseButtonReleasedEvent(message, releaseRay));
            return;
        }

        if (TryCreateWorldRay(message.Position, out var ray))
        {
            OnMouseButtonReleased(message.Mouse, message.Button, message.Position, ray);
            PublishInputEvent(new ViewMouseButtonReleasedEvent(message, ray));
        }
    }

    /// <summary>Publishes a world-ray event for mouse-wheel movement inside this view.</summary>
    /// <param name="message">The source mouse-wheel movement.</param>
    private void OnMouseWheel(MouseWheelEvent message)
    {
        UpdateMouseInteractionPositions(message.Mouse, message.Position);
        if (TryCreateWorldRay(message.Position, out var ray))
        {
            OnMouseWheel(message.Mouse, message.Delta, message.Position, ray);
            PublishInputEvent(new ViewMouseWheelEvent(message, ray));
        }
    }

    /// <summary>Cancels all accepted interactions for the source mouse regardless of its position.</summary>
    /// <param name="message">The source mouse-cancellation event.</param>
    private void OnMouseCanceled(MouseCanceledEvent message)
    {
        if (!TryCreateWorldRay(message.Position, out _))
            UpdateMouseRegion(new MouseMovedEvent(message.Mouse, message.Position), false, default);

        if (!CancelMouseInteractions(message) && TryCreateWorldRay(message.Position, out var ray))
        {
            OnMouseCanceled(message.Mouse, message.Position, ray);
            PublishInputEvent(new ViewMouseCanceledEvent(message, ray));
        }
    }

    /// <summary>Exits and cancels this view's interactions for a disconnected mouse.</summary>
    /// <param name="message">The disconnected device and its final pointer position.</param>
    private void OnMouseDisconnected(MouseDisconnectedEvent message)
    {
        UpdateMouseRegion(new MouseMovedEvent(message.Mouse, message.Position), false, default);
        CancelMouseInteractions(new MouseCanceledEvent(message.Mouse, message.Position));
    }

    /// <summary>Retains the latest reported position for every captured button on a mouse.</summary>
    /// <param name="mouse">The mouse reporting the position.</param>
    /// <param name="position">The absolute screen-space pointer position.</param>
    private void UpdateMouseInteractionPositions(IMouseInputDevice mouse, Vector2D<float> position)
    {
        if (_mouseInteractions.Count == 0)
            return;

        var keys = _mouseInteractions
            .Keys.Where(key => ReferenceEquals(key.Mouse, mouse))
            .ToArray();
        foreach (var key in keys)
            _mouseInteractions[key] = (_mouseInteractions[key].StartRay, position);
    }

    /// <summary>Cancels all captured buttons for one mouse with a single notification.</summary>
    /// <param name="message">The device and pointer position associated with cancellation.</param>
    /// <returns>Whether any captured interactions were canceled.</returns>
    private bool CancelMouseInteractions(MouseCanceledEvent message)
    {
        var interactions = _mouseInteractions
            .Where(item => ReferenceEquals(item.Key.Mouse, message.Mouse))
            .ToArray();
        if (interactions.Length > 0)
        {
            foreach (var interaction in interactions)
                _mouseInteractions.Remove(interaction.Key);

            var cancellationRay = TryProjectWorldRay(message.Position, out var projectedRay)
                ? projectedRay
                : interactions[^1].Value.StartRay;
            OnMouseCanceled(message.Mouse, message.Position, cancellationRay);
            PublishInputEvent(new ViewMouseCanceledEvent(message, cancellationRay));
            return true;
        }

        return false;
    }

    /// <summary>Notifies consumers before discarding interactions when input subscriptions are removed.</summary>
    private void CancelAllInteractions()
    {
        foreach (var interaction in _mouseInteractions.ToArray())
            CancelMouseInteractions(
                new MouseCanceledEvent(interaction.Key.Mouse, interaction.Value.Position)
            );

        foreach (var interaction in _touchInteractions)
        {
            var ray = TryProjectWorldRay(interaction.Value.Position, out var projectedRay)
                ? projectedRay
                : interaction.Value.StartRay;
            OnTouchCanceled(
                interaction.Key.TouchScreen,
                interaction.Key.TouchId,
                interaction.Value.Position,
                ray
            );
            PublishInputEvent(
                new ViewTouchCanceledEvent(
                    interaction.Key.TouchScreen,
                    interaction.Key.TouchId,
                    interaction.Value.Position,
                    ray
                )
            );
        }
        _touchInteractions.Clear();
    }

    /// <summary>Publishes a world-ray event for a touch starting inside this view.</summary>
    /// <param name="message">The source touch-start event.</param>
    private void OnTouchStarted(TouchStartedEvent message)
    {
        if (TryCreateWorldRay(message.Position, out var ray))
        {
            _touchInteractions[(message.TouchScreen, message.TouchId)] = (ray, message.Position);
            _insideTouches[(message.TouchScreen, message.TouchId)] = message.Position;
            OnTouchStarted(message.TouchScreen, message.TouchId, message.Position, ray);
            PublishInputEvent(new ViewTouchStartedEvent(message, ray));
        }
    }

    /// <summary>Reports boundary exits and movement only for touch interactions this view owns.</summary>
    /// <param name="message">The source touch-movement event.</param>
    private void OnTouchMoved(TouchMovedEvent message)
    {
        var key = (message.TouchScreen, message.TouchId);
        if (!_touchInteractions.TryGetValue(key, out var interaction))
            return;

        _touchInteractions[key] = (interaction.StartRay, message.Position);
        var inside = TryCreateWorldRay(message.Position, out var ray);
        if (inside)
        {
            _insideTouches[key] = message.Position;
            OnTouchMoved(message.TouchScreen, message.TouchId, message.Position, ray);
            PublishInputEvent(new ViewTouchMovedEvent(message, ray));
            return;
        }

        if (_insideTouches.Remove(key))
        {
            OnTouchExited(message.TouchScreen, message.TouchId, message.Position);
            PublishInputEvent(
                new ViewTouchExitedEvent(
                    message.TouchScreen,
                    message.TouchId,
                    message.Position
                )
            );
        }

        if (IsInputActive && TryProjectWorldRay(message.Position, out ray))
        {
            OnTouchMoved(message.TouchScreen, message.TouchId, message.Position, ray);
            PublishInputEvent(new ViewTouchMovedEvent(message, ray));
        }
    }

    /// <summary>Terminates a touch interaction this view accepted, regardless of its current position.</summary>
    /// <param name="message">The source touch-end event.</param>
    private void OnTouchEnded(TouchEndedEvent message)
    {
        _insideTouches.Remove((message.TouchScreen, message.TouchId));
        if (_touchInteractions.Remove((message.TouchScreen, message.TouchId), out var interaction))
        {
            var endRay = TryProjectWorldRay(message.Position, out var projectedRay)
                ? projectedRay
                : interaction.StartRay;
            OnTouchEnded(message.TouchScreen, message.TouchId, message.Position, endRay);
            PublishInputEvent(new ViewTouchEndedEvent(message, endRay));
        }
    }

    /// <summary>Gets whether this view can publish pointer movement or start new interactions.</summary>
    private bool IsInputActive =>
        PropagateInputEvents && IsActivated && IsEffectivelyVisible && IsEffectivelyEnabled;

    /// <summary>Reports exits when layout, GUI targeting, or view state invalidates a known pointer position.</summary>
    internal void RefreshPointerRegions()
    {
        for (var index = _hoveringMice.Count - 1; index >= 0; index--)
        {
            var message = _hoveringMice[index];
            if (TryCreateWorldRay(message.Position, out _))
                continue;

            _hoveringMice.RemoveAt(index);
            OnMouseExited(message.Mouse, message.Position);
            PublishInputEvent(new ViewMouseExitedEvent(message));
        }

        foreach (var touch in _insideTouches.ToArray())
        {
            if (TryCreateWorldRay(touch.Value, out _))
                continue;

            _insideTouches.Remove(touch.Key);
            OnTouchExited(touch.Key.TouchScreen, touch.Key.TouchId, touch.Value);
            PublishInputEvent(
                new ViewTouchExitedEvent(
                    touch.Key.TouchScreen,
                    touch.Key.TouchId,
                    touch.Value
                )
            );
        }
    }

    /// <summary>Reports all remaining boundary exits before the view stops observing pointer input.</summary>
    private void ExitAllPointers()
    {
        foreach (var message in _hoveringMice)
        {
            OnMouseExited(message.Mouse, message.Position);
            PublishInputEvent(new ViewMouseExitedEvent(message));
        }
        _hoveringMice.Clear();

        foreach (var touch in _insideTouches)
        {
            OnTouchExited(touch.Key.TouchScreen, touch.Key.TouchId, touch.Value);
            PublishInputEvent(
                new ViewTouchExitedEvent(
                    touch.Key.TouchScreen,
                    touch.Key.TouchId,
                    touch.Value
                )
            );
        }
        _insideTouches.Clear();
    }

    /// <summary>Creates a camera ray for a screen point visible through this view.</summary>
    /// <param name="screenPosition">The absolute screen-space pointer position.</param>
    /// <param name="ray">The projected world-space ray, or the default value when invalid.</param>
    /// <returns>Whether the position maps to a valid ray inside the view.</returns>
    private bool TryCreateWorldRay(Vector2D<float> screenPosition, out Ray3D<float> ray)
    {
        ray = default;
        var viewport = ViewComponent.ViewportRegion;
        if (
            !IsInputActive
            || Camera is null
            || viewport.Size.X <= 0
            || viewport.Size.Y <= 0
            || !float.IsFinite(screenPosition.X)
            || !float.IsFinite(screenPosition.Y)
            || !ContainsScreenPosition(ViewComponent.ClippingRegion, screenPosition)
            || !ContainsScreenPosition(viewport, screenPosition)
            || _pointerHitTest?.Invoke(screenPosition) != true
        )
            return false;

        return TryProjectWorldRay(screenPosition, out ray);
    }

    /// <summary>Projects a pointer position without containment or GUI targeting restrictions.</summary>
    /// <param name="screenPosition">The absolute pointer position, which may be outside the viewport.</param>
    /// <param name="ray">The projected world ray when the camera and position remain valid.</param>
    /// <returns>Whether a valid ray can be projected; termination handlers otherwise use the interaction's start ray.</returns>
    private bool TryProjectWorldRay(Vector2D<float> screenPosition, out Ray3D<float> ray)
    {
        ray = default;
        var viewport = ViewComponent.ViewportRegion;
        if (
            Camera is null
            || viewport.Size.X <= 0
            || viewport.Size.Y <= 0
            || !float.IsFinite(screenPosition.X)
            || !float.IsFinite(screenPosition.Y)
        )
            return false;

        var localPosition =
            screenPosition - new Vector2D<float>(viewport.Origin.X, viewport.Origin.Y);
        var worldRay = Camera.ScreenToWorldRay(localPosition, viewport.Size.X, viewport.Size.Y);
        if (
            !IsFinite(worldRay.Origin)
            || !IsFinite(worldRay.Direction)
            || worldRay.Direction.LengthSquared <= 0f
        )
            return false;

        ray = worldRay;
        return true;
    }

    /// <summary>Determines whether an absolute screen position lies within an integer viewport.</summary>
    /// <param name="region">The viewport or clipping region.</param>
    /// <param name="position">The absolute screen position.</param>
    /// <returns>Whether the region contains the position.</returns>
    private static bool ContainsScreenPosition(Rectangle<int> region, Vector2D<float> position) =>
        region.Size.X > 0
        && region.Size.Y > 0
        && position.X >= region.Origin.X
        && position.X < (double)region.Origin.X + region.Size.X
        && position.Y >= region.Origin.Y
        && position.Y < (double)region.Origin.Y + region.Size.Y;

    /// <summary>Determines whether every component of a vector is finite.</summary>
    /// <param name="vector">The vector to check.</param>
    /// <returns>Whether all components are finite.</returns>
    private static bool IsFinite(Vector3D<float> vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    /// <summary>Queues a projected input notification for non-UI event subscribers.</summary>
    /// <param name="inputEvent">The view-specific event to queue.</param>
    private void PublishInputEvent(IEvent inputEvent) => _inputEventHub?.Publish(inputEvent);

    /// <summary>Subscribes to visibility changes throughout the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (ISceneNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not IObservable observable)
                continue;

            observable.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(observable);
        }
    }

    /// <summary>Refreshes the viewport when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The name of the changed ancestor property.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            UpdateViewport();
        else if (propertyName == nameof(IsEnabled))
            RefreshPointerRegions();
    }

    /// <summary>Synchronizes the renderer's clipping rectangle with visible arranged bounds.</summary>
    private void UpdateViewport()
    {
        var bounds = IsEffectivelyVisible
            ? Bounds
            : new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero);
        var clippingRegion = ToClippingRegion(bounds);
        ViewComponent.ClippingRegion = clippingRegion;
        ViewComponent.ViewportRegion = CalculateViewportRegion(clippingRegion);
        RefreshPointerRegions();
    }

    /// <summary>Calculates the pixel viewport while retaining the camera projection aspect ratio.</summary>
    /// <param name="clippingRegion">The arranged bounds used as the scissor region.</param>
    /// <returns>The viewport region used for projection mapping.</returns>
    private Rectangle<int> CalculateViewportRegion(Rectangle<int> clippingRegion)
    {
        if (
            SizingMode == ViewSizingMode.Stretch
            || Camera is null
            || clippingRegion.Size.X <= 0
            || clippingRegion.Size.Y <= 0
            || !float.IsFinite(Camera.AspectRatio)
            || Camera.AspectRatio <= 0f
        )
            return clippingRegion;

        var boundsAspect = (float)clippingRegion.Size.X / clippingRegion.Size.Y;
        var viewportWidth = clippingRegion.Size.X;
        var viewportHeight = clippingRegion.Size.Y;

        if (
            (SizingMode == ViewSizingMode.Fit && boundsAspect > Camera.AspectRatio)
            || (SizingMode == ViewSizingMode.Fill && boundsAspect < Camera.AspectRatio)
        )
            viewportWidth = (int)MathF.Round(viewportHeight * Camera.AspectRatio);
        else
            viewportHeight = (int)MathF.Round(viewportWidth / Camera.AspectRatio);

        var x = clippingRegion.Origin.X + (clippingRegion.Size.X - viewportWidth) / 2;
        var y = clippingRegion.Origin.Y + (clippingRegion.Size.Y - viewportHeight) / 2;
        return new Rectangle<int>(x, y, viewportWidth, viewportHeight);
    }

    /// <summary>Converts screen-space bounds to an integer scissor rectangle without expanding them.</summary>
    /// <param name="bounds">The arranged screen-space bounds.</param>
    /// <returns>The integer rectangle constrained to the bounds' interior.</returns>
    private static Rectangle<int> ToClippingRegion(Rectangle<float> bounds)
    {
        if (
            !float.IsFinite(bounds.Origin.X)
            || !float.IsFinite(bounds.Origin.Y)
            || !float.IsFinite(bounds.Size.X)
            || !float.IsFinite(bounds.Size.Y)
            || bounds.Size.X <= 0f
            || bounds.Size.Y <= 0f
        )
            return EmptyBounds;

        var left = (int)Math.Clamp(Math.Ceiling((double)bounds.Origin.X), 0d, int.MaxValue);
        var top = (int)Math.Clamp(Math.Ceiling((double)bounds.Origin.Y), 0d, int.MaxValue);
        var right = (int)
            Math.Clamp(Math.Floor((double)bounds.Origin.X + bounds.Size.X), left, int.MaxValue);
        var bottom = (int)
            Math.Clamp(Math.Floor((double)bounds.Origin.Y + bounds.Size.Y), top, int.MaxValue);

        return new Rectangle<int>(left, top, right - left, bottom - top);
    }

    /// <summary>Gets the empty clipping rectangle used to suppress rendering.</summary>
    private static Rectangle<int> EmptyBounds => new(0, 0, 0, 0);
}
