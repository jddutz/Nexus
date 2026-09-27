namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse button has been released.
/// </summary>
/// <param name="mouse">The mouse that reported the release.</param>
/// <param name="button">The released button.</param>
/// <param name="position">The mouse position when the button was released.</param>
public sealed class MouseButtonReleasedEvent(
    IMouseInputDevice mouse,
    MouseButtonEnum button,
    Vector2D<float> position
)
    : IEvent
{
    /// <summary>
    /// Gets the mouse that reported the release.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>
    /// Gets the released button.
    /// </summary>
    public MouseButtonEnum Button { get; } = button;

    /// <summary>
    /// Gets the mouse position when the button was released.
    /// </summary>
    public Vector2D<float> Position { get; } = position;
}
