namespace Nexus.Input;

/// <summary>
/// Represents a keyboard becoming available to the input system.
/// </summary>
/// <param name="keyboard">The connected keyboard.</param>
public sealed class KeyboardConnectedEvent(IKeyboardInputDevice keyboard) : IEvent
{
    /// <summary>Gets the connected keyboard.</summary>
    public IKeyboardInputDevice Keyboard { get; } = keyboard;
}

/// <summary>
/// Represents a keyboard becoming unavailable to the input system.
/// </summary>
/// <param name="keyboard">The disconnected keyboard.</param>
public sealed class KeyboardDisconnectedEvent(IKeyboardInputDevice keyboard) : IEvent
{
    /// <summary>Gets the disconnected keyboard.</summary>
    public IKeyboardInputDevice Keyboard { get; } = keyboard;
}

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
