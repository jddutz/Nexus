namespace Nexus.Input;

/// <summary>
/// Tracks keyboard devices and aggregate key state.
/// </summary>
public class KeyboardInputState : IKeyboardInputState
{
    /// <summary>
    /// Gets the registered keyboards by device identifier.
    /// </summary>
    private readonly Dictionary<InputDeviceId, IKeyboardInputDevice> _keyboards = [];

    /// <summary>
    /// Gets the registered keyboard for the specified device identifier.
    /// </summary>
    /// <param name="id">The identifier of the keyboard to retrieve.</param>
    /// <returns>The registered keyboard with the specified identifier.</returns>
    /// <exception cref="KeyNotFoundException">No keyboard is registered with the specified identifier.</exception>
    public IKeyboardInputDevice this[InputDeviceId id] => _keyboards[id];

    /// <summary>
    /// Registers a keyboard device with this input state.
    /// </summary>
    /// <param name="keyboard">The keyboard device to register.</param>
    public void Register(IKeyboardInputDevice keyboard)
    {
        _keyboards[keyboard.Id] = keyboard;
    }

    /// <summary>
    /// Unregisters a keyboard device from this input state.
    /// </summary>
    /// <param name="keyboard">The keyboard device to unregister.</param>
    public void Unregister(IKeyboardInputDevice keyboard)
    {
        _keyboards.Remove(keyboard.Id);
    }

    /// <inheritdoc />
    public bool IsKeyDown(KeyEnum key) =>
        _keyboards.Values.Any(kb => kb.IsConnected && kb.IsKeyDown(key));
}
