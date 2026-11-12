namespace Nexus.Input.Events;

/// <summary>Provides a camera ray for a mouse-button press within a view.</summary>
/// <param name="inputEvent">The source mouse-button press.</param>
/// <param name="worldRay">The camera ray through the pointer position.</param>
public sealed class ViewMouseButtonPressedEvent(
    MouseButtonPressedEvent inputEvent,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the source mouse-button press.</summary>
    public MouseButtonPressedEvent InputEvent { get; } = inputEvent;

    /// <summary>Gets the camera ray through the pointer position.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
