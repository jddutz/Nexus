namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for mouse-wheel movement within a view.</summary>
/// <param name="inputEvent">The source mouse-wheel movement.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseWheelEvent(
    MouseWheelEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source mouse-wheel movement.</summary>
    public MouseWheelEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
