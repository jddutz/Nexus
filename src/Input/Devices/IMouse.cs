namespace Nexus.Input.Devices;

/// <summary>
/// Provides state and transition events for a mouse device.
/// </summary>
public interface IMouseInputDevice : IInputDevice
{
    /// <summary>
    /// Gets the current mouse position in window coordinates.
    /// </summary>
    Vector2D<float> Position { get; }

    /// <summary>
    /// Occurs when the mouse position changes.
    /// </summary>
    event Action<IMouseInputDevice, Vector2D<float>>? Moved;

    /// <summary>
    /// Occurs when a mouse button is pressed.
    /// </summary>
    event Action<IMouseInputDevice, MouseButtonEnum>? ButtonPressed;

    /// <summary>
    /// Occurs when a mouse button is released.
    /// </summary>
    event Action<IMouseInputDevice, MouseButtonEnum>? ButtonReleased;

    /// <summary>
    /// Occurs when the mouse wheel moves.
    /// </summary>
    event Action<IMouseInputDevice, Vector2D<float>>? WheelMoved;

    /// <summary>
    /// Gets whether the specified mouse button is currently down.
    /// </summary>
    /// <param name="button">The button to check.</param>
    /// <returns><see langword="true"/> if the button is down; otherwise, <see langword="false"/>.</returns>
    bool IsButtonDown(MouseButtonEnum button);
}
