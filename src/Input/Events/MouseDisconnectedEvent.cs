namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse has disconnected.
/// </summary>
/// <param name="mouse">The disconnected mouse.</param>
public sealed class MouseDisconnectedEvent(IMouseInputDevice mouse) : IEvent
{
    /// <summary>
    /// Gets the disconnected mouse.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;
}
