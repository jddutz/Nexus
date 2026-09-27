namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse button has been pressed.
/// </summary>
/// <param name="mouse">The mouse that reported the press.</param>
/// <param name="button">The pressed button.</param>
public sealed class MouseButtonPressedEvent(IMouseInputDevice mouse, MouseButtonEnum button)
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
}
