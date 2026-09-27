namespace Nexus.Input;

/// <summary>
/// Provides access to registered mice and their aggregate state.
/// </summary>
public interface IMouseInputState
{
    /// <summary>
    /// Gets the registered mouse for the specified device identifier.
    /// </summary>
    /// <param name="id">The identifier of the mouse to retrieve.</param>
    /// <returns>The registered mouse with the specified identifier.</returns>
    /// <exception cref="KeyNotFoundException">No mouse is registered with the specified identifier.</exception>
    IMouseInputDevice this[InputDeviceId id] { get; }

    /// <summary>
    /// Gets the connected mice currently registered with this input system.
    /// </summary>
    IReadOnlyCollection<IMouseInputDevice> Mice { get; }

    /// <summary>
    /// Gets the current position of the first connected mouse, or zero if none is connected.
    /// </summary>
    Vector2D<float> Position { get; }

    /// <summary>
    /// Gets whether the specified button is down on any connected mouse.
    /// </summary>
    /// <param name="button">The button to check.</param>
    /// <returns><see langword="true"/> if any connected mouse has the button down.</returns>
    bool IsButtonDown(MouseButtonEnum button);
}
