namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for a touch point starting within a view.</summary>
/// <param name="inputEvent">The source touch-start event.</param>
/// <param name="worldRay">The camera ray through the touch position.</param>
public sealed class ViewTouchStartedEvent(
    TouchStartedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source touch-start event.</summary>
    public TouchStartedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the touch position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
