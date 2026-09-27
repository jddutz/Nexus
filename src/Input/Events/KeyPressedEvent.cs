namespace Nexus.Input.Events;

/// <summary>
/// Represents a key being pressed on a keyboard.
/// </summary>
/// <param name="keyboard">The keyboard reporting the key press.</param>
/// <param name="key">The key that was pressed.</param>
public sealed class KeyPressedEvent(IKeyboardInputDevice keyboard, KeyEnum key) : IEvent
{
    /// <summary>Gets the keyboard reporting the key press.</summary>
    public IKeyboardInputDevice Keyboard { get; } = keyboard;

    /// <summary>Gets the pressed key.</summary>
    public KeyEnum Key { get; } = key;
}