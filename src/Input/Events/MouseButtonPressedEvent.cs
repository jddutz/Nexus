namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse button has been pressed.
/// </summary>
/// <param name="mouse">The mouse that reported the press.</param>
/// <param name="button">The pressed button.</param>
/// <param name="position">The mouse position when the button was pressed.</param>
public sealed class MouseButtonPressedEvent(
    IMouseInputDevice mouse,
    MouseButtonEnum button,
    Vector2D<float> position
)
    : IEvent
{
    /// <summary>
    /// Gets the mouse that reported the press.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>
    /// Gets the pressed button.
    /// </summary>
    public MouseButtonEnum Button { get; } = button;

    /// <summary>
    /// Gets the mouse position when the button was pressed.
    /// </summary>
    public Vector2D<float> Position { get; } = position;
}
