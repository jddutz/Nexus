namespace Nexus.GUI.Events;

using Nexus.Core.Events;
using Nexus.GUI.Elements;
using Nexus.Input.Devices;
using Nexus.Input.Events;
using Silk.NET.Maths;

/// <summary>Reports a mouse entering a view's interactive region.</summary>
/// <param name="view">The view the mouse entered.</param>
/// <param name="inputEvent">The source mouse movement.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseEnteredEvent(
    View view,
    MouseMovedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view the mouse entered.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source mouse movement.</summary>
    public MouseMovedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Reports a mouse leaving a view's interactive region without ending its interaction.</summary>
/// <param name="view">The view the mouse exited.</param>
/// <param name="inputEvent">The movement or last known pointer position when the region became unavailable.</param>
public sealed class ViewMouseExitedEvent(View view, MouseMovedEvent inputEvent) : IEvent
{
    /// <summary>Gets the view the mouse exited.</summary>
    public View View { get; } = view;

    /// <summary>Gets the movement or last known pointer position associated with the exit.</summary>
    public MouseMovedEvent InputEvent { get; } = inputEvent;
}

/// <summary>Provides a camera ray for mouse movement inside a view or during a captured drag outside it.</summary>
/// <param name="view">The view that projected the pointer position.</param>
/// <param name="inputEvent">The source mouse movement.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseMovedEvent(
    View view,
    MouseMovedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the pointer position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source mouse movement.</summary>
    public MouseMovedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Provides a camera ray for a mouse-button press within a view.</summary>
/// <param name="view">The view that projected the pointer position.</param>
/// <param name="inputEvent">The source mouse-button press.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseButtonPressedEvent(
    View view,
    MouseButtonPressedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the pointer position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source mouse-button press.</summary>
    public MouseButtonPressedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Provides a camera ray for a release within a view or ending a mouse interaction that began there.</summary>
/// <remarks>Captured releases may be outside the view. If projection is unavailable, the press ray is retained.</remarks>
/// <param name="view">The view that projected the pointer position.</param>
/// <param name="inputEvent">The source mouse-button release.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseButtonReleasedEvent(
    View view,
    MouseButtonReleasedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the pointer position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source mouse-button release.</summary>
    public MouseButtonReleasedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Provides a camera ray for mouse-wheel movement within a view.</summary>
/// <param name="view">The view that projected the pointer position.</param>
/// <param name="inputEvent">The source mouse-wheel movement.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseWheelEvent(
    View view,
    MouseWheelEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the pointer position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source mouse-wheel movement.</summary>
    public MouseWheelEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Provides a camera ray when a view's mouse interaction is canceled, even outside the viewport.</summary>
/// <remarks>
/// Cancellation is also synthesized when propagation stops, the view is unregistered, or the mouse disconnects.
/// If projection is unavailable, an accepted press ray is retained.
/// </remarks>
/// <param name="view">The view that projected the pointer position.</param>
/// <param name="inputEvent">The source mouse-cancellation event.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseCanceledEvent(
    View view,
    MouseCanceledEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the pointer position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source or synthesized mouse-cancellation event.</summary>
    public MouseCanceledEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Provides a camera ray for a touch point starting within a view.</summary>
/// <param name="view">The view that projected the touch position.</param>
/// <param name="inputEvent">The source touch-start event.</param>
/// <param name="worldRay">The camera ray through the touch position.</param>
public sealed class ViewTouchStartedEvent(
    View view,
    TouchStartedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the touch position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source touch-start event.</summary>
    public TouchStartedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the touch position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Reports an owned touch leaving a view's interactive region without ending its interaction.</summary>
/// <param name="view">The view the touch exited.</param>
/// <param name="touchScreen">The touch device that owns the touch.</param>
/// <param name="touchId">The device-local identifier of the active touch.</param>
/// <param name="position">The touch position or last known position when the region became unavailable.</param>
public sealed class ViewTouchExitedEvent(
    View view,
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position
) : IEvent
{
    /// <summary>Gets the view the touch exited.</summary>
    public View View { get; } = view;

    /// <summary>Gets the touch device that owns the touch.</summary>
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;

    /// <summary>Gets the device-local identifier of the active touch.</summary>
    public int TouchId { get; } = touchId;

    /// <summary>Gets the touch position associated with the exit.</summary>
    public Vector2D<float> Position { get; } = position;
}

/// <summary>Provides a camera ray for an owned touch moving inside or outside its originating view.</summary>
/// <param name="view">The view that projected the touch position.</param>
/// <param name="inputEvent">The source touch-movement event.</param>
/// <param name="worldRay">The camera ray through the touch position.</param>
public sealed class ViewTouchMovedEvent(
    View view,
    TouchMovedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the touch position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source touch-movement event.</summary>
    public TouchMovedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the touch position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Provides a camera ray for the end of a touch interaction that began within a view.</summary>
/// <remarks>Captured ends may be outside the view. If projection is unavailable, the touch-start ray is retained.</remarks>
/// <param name="view">The view that projected the touch position.</param>
/// <param name="inputEvent">The source touch-end event.</param>
/// <param name="worldRay">The camera ray through the touch position.</param>
public sealed class ViewTouchEndedEvent(
    View view,
    TouchEndedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that projected the touch position.</summary>
    public View View { get; } = view;

    /// <summary>Gets the source touch-end event.</summary>
    public TouchEndedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the touch position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}

/// <summary>Reports an owned touch canceled when the view stops propagating input or is unregistered.</summary>
/// <remarks>If projection is unavailable, the accepted touch-start ray is retained.</remarks>
/// <param name="view">The view that owned the touch interaction.</param>
/// <param name="touchScreen">The touch device that owns the touch.</param>
/// <param name="touchId">The device-local identifier of the canceled touch.</param>
/// <param name="position">The last reported screen-space touch position.</param>
/// <param name="worldRay">The camera ray through the last position, or the accepted start ray.</param>
public sealed class ViewTouchCanceledEvent(
    View view,
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the view that owned the touch interaction.</summary>
    public View View { get; } = view;

    /// <summary>Gets the touch device that owns the touch.</summary>
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;

    /// <summary>Gets the device-local identifier of the canceled touch.</summary>
    public int TouchId { get; } = touchId;

    /// <summary>Gets the last reported screen-space touch position.</summary>
    public Vector2D<float> Position { get; } = position;

    /// <summary>Gets the camera ray through the last position, or the accepted start ray.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
