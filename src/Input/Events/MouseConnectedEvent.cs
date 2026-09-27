namespace Nexus.Input.Events;

/// <summary>
/// Indicates that a mouse has connected.
/// </summary>
/// <param name="mouse">The connected mouse.</param>
/// <param name="position">The mouse position when it connected.</param>
public sealed class MouseConnectedEvent(IMouseInputDevice mouse, Vector2D<float> position) : IEvent
{
    /// <summary>
    /// Gets the connected mouse.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>
    /// Gets the mouse position when it connected.
    /// </summary>
    public Vector2D<float> Position { get; } = position;
}
