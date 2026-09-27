namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse position has changed.
/// </summary>
/// <param name="mouse">The mouse that moved.</param>
/// <param name="position">The new mouse position.</param>
public sealed class MouseMovedEvent(IMouseInputDevice mouse, Vector2D<float> position) : IEvent
{
    /// <summary>
    /// Gets the mouse that moved.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>
    /// Gets the new mouse position.
    /// </summary>
    public Vector2D<float> Position { get; } = position;
}
