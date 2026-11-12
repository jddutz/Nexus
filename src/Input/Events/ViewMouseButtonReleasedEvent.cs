namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for a release within a view or ending a mouse interaction that began there.</summary>
/// <remarks>Captured releases may be outside the view. If projection is unavailable, the press ray is retained.</remarks>
/// <param name="inputEvent">The source mouse-button release.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseButtonReleasedEvent(
    MouseButtonReleasedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source mouse-button release.</summary>
    public MouseButtonReleasedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
