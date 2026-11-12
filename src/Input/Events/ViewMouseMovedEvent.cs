namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for mouse movement inside a view or during a captured drag outside it.</summary>
/// <param name="inputEvent">The source mouse movement.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseMovedEvent(
    MouseMovedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source mouse movement.</summary>
    public MouseMovedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
