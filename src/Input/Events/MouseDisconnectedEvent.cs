namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse has disconnected.
/// </summary>
/// <param name="mouse">The disconnected mouse.</param>
/// <param name="position">The mouse position when it disconnected.</param>
public sealed class MouseDisconnectedEvent(IMouseInputDevice mouse, Vector2D<float> position) : IEvent
{
    /// <summary>
    /// Gets the disconnected mouse.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>
    /// Gets the mouse position when it disconnected.
    /// </summary>
    public Vector2D<float> Position { get; } = position;
}
