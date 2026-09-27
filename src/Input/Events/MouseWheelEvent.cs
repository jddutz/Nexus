namespace Nexus.Input.Events;

/// <summary>
/// Indicates that the mouse wheel has moved.
/// </summary>
/// <param name="mouse">The mouse that reported the wheel movement.</param>
/// <param name="delta">The scroll delta.</param>
/// <param name="position">The mouse position when the wheel moved.</param>
public sealed class MouseWheelEvent(
    IMouseInputDevice mouse,
    Vector2D<float> delta,
    Vector2D<float> position
) : IEvent
{
    /// <summary>
    /// Gets the mouse that reported the wheel movement.
    /// </summary>
    public IMouseInputDevice Mouse { get; } = mouse;

    /// <summary>
    /// Gets the scroll delta.
    /// </summary>
    public Vector2D<float> Delta { get; } = delta;

    /// <summary>
    /// Gets the mouse position when the wheel moved.
    /// </summary>
    public Vector2D<float> Position { get; } = position;
}
