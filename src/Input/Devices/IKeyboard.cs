namespace Nexus.Input.Devices;

public interface IKeyboardInputDevice : IInputDevice
{
    /// <summary>
    /// Occurs when a key is pressed on this keyboard.
    /// </summary>
    event Action<IKeyboardInputDevice, KeyEnum>? KeyPressed;

    /// <summary>
    /// Occurs when a key is released on this keyboard.
    /// </summary>
    event Action<IKeyboardInputDevice, KeyEnum>? KeyReleased;

    /// <summary>
    /// Gets whether the specified key is currently down.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> if the key is down; otherwise, <see langword="false"/>.</returns>
    bool IsKeyDown(KeyEnum key);
}
