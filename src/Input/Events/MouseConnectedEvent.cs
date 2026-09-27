namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse has connected.
/// </summary>
/// <param name="mouse">The connected mouse.</param>
public sealed class MouseConnectedEvent(IMouseInputDevice mouse) : IEvent
{
    /// <summary>
    /// Gets the connected mouse.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;
}
