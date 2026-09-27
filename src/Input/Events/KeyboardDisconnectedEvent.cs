namespace Nexus.Input.Events;

/// <summary>
/// Represents a keyboard becoming unavailable to the input system.
/// </summary>
/// <param name="keyboard">The disconnected keyboard.</param>
public sealed class KeyboardDisconnectedEvent(IKeyboardInputDevice keyboard) : IEvent
{
    /// <summary>Gets the disconnected keyboard.</summary>
    public IKeyboardInputDevice Keyboard { get; } = keyboard;
}
