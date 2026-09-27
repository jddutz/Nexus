namespace Nexus.Input.Events;

/// <summary>
/// Represents a keyboard becoming available to the input system.
/// </summary>
/// <param name="keyboard">The connected keyboard.</param>
public sealed class KeyboardConnectedEvent(IKeyboardInputDevice keyboard) : IEvent
{
    /// <summary>Gets the connected keyboard.</summary>
    public IKeyboardInputDevice Keyboard { get; } = keyboard;
}
