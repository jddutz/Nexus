namespace Nexus.Input;

/// <summary>
/// Provides access to registered keyboard devices and their aggregate key state.
/// </summary>
public interface IKeyboardInputState
{
    /// <summary>
    /// Gets the registered keyboard for the specified device identifier.
    /// </summary>
    /// <param name="id">The identifier of the keyboard to retrieve.</param>
    /// <returns>The registered keyboard with the specified identifier.</returns>
    /// <exception cref="KeyNotFoundException">No keyboard is registered with the specified identifier.</exception>
    IKeyboardInputDevice this[InputDeviceId id] { get; }

    /// <summary>
    /// Gets whether the specified key is down on any registered keyboard.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> if the key is down on any registered keyboard; otherwise, <see langword="false"/>.</returns>
    bool IsKeyDown(KeyEnum key);
}
