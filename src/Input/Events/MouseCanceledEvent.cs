namespace Nexus.Input.Events;

/// <summary>Indicates that an active mouse interaction was interrupted.</summary>
/// <param name="mouse">The mouse whose interaction was interrupted.</param>
/// <param name="position">The mouse position when cancellation occurred.</param>
public sealed class MouseCanceledEvent(IMouseInputDevice mouse, Vector2D<float> position) : IEvent
{
    /// <summary>Gets the mouse whose interaction was interrupted.</summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>Gets the mouse position when cancellation occurred.</summary>
    public Vector2D<float> Position { get; } = position;
}
