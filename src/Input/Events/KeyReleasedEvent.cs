namespace Nexus.Input.Events;

/// <summary>
/// Represents a key being released on a keyboard.
/// </summary>
/// <param name="keyboard">The keyboard reporting the key release.</param>
/// <param name="key">The key that was released.</param>
public sealed class KeyReleasedEvent(IKeyboardInputDevice keyboard, KeyEnum key) : IEvent
{
    /// <summary>Gets the keyboard reporting the key release.</summary>
    public IKeyboardInputDevice Keyboard { get; } = keyboard;

    /// <summary>Gets the released key.</summary>
    public KeyEnum Key { get; } = key;
}