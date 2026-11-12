namespace Nexus.Input.Events;

/// <summary>Provides a camera ray when a view's mouse interaction is canceled, even outside the viewport.</summary>
/// <remarks>
/// Cancellation is also synthesized when propagation stops, the view is unregistered, or the mouse disconnects.
/// If projection is unavailable, an accepted press ray is retained.
/// </remarks>
/// <param name="inputEvent">The source mouse-cancellation event.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseCanceledEvent(
    MouseCanceledEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source or synthesized mouse-cancellation event.</summary>
    public MouseCanceledEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
