namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for an owned touch moving inside or outside its originating view.</summary>
/// <param name="inputEvent">The source touch-movement event.</param>
/// <param name="worldRay">The camera ray through the touch position.</param>
public sealed class ViewTouchMovedEvent(
    TouchMovedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source touch-movement event.</summary>
    public TouchMovedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the touch position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
