namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for the end of a touch interaction that began within a view.</summary>
/// <remarks>Captured ends may be outside the view. If projection is unavailable, the touch-start ray is retained.</remarks>
/// <param name="inputEvent">The source touch-end event.</param>
/// <param name="worldRay">The camera ray through the touch position.</param>
public sealed class ViewTouchEndedEvent(
    TouchEndedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source touch-end event.</summary>
    public TouchEndedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the touch position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
