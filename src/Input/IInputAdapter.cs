namespace Nexus.Input;

public interface IInputAdapter
{
    IReadOnlyCollection<IKeyboardInputDevice> Keyboards { get; }

    /// <summary>
    /// Gets the mice currently exposed by this adapter.
    /// </summary>
    IReadOnlyCollection<IMouseInputDevice> Mice { get; }

    /// <summary>
    /// Occurs when a keyboard is connected.
    /// </summary>
    event Action<IKeyboardInputDevice>? KeyboardConnected;

    /// <summary>
    /// Occurs when a keyboard is disconnected.
    /// </summary>
    event Action<IKeyboardInputDevice>? KeyboardDisconnected;

    /// <summary>
    /// Occurs when a mouse is connected.
    /// </summary>
    event Action<IMouseInputDevice>? MouseConnected;

    /// <summary>
    /// Occurs when a mouse is disconnected.
    /// </summary>
    event Action<IMouseInputDevice>? MouseDisconnected;
}
