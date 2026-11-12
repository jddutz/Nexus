namespace Nexus.Input.Events;

/// <summary>Reports a mouse entering a view's interactive region.</summary>
/// <param name="inputEvent">The source mouse movement.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseEnteredEvent(
    MouseMovedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source mouse movement.</summary>
    public MouseMovedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
