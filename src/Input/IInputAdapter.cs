namespace Nexus.Input;

public interface IInputAdapter
{
    IReadOnlyCollection<IKeyboardInputDevice> Keyboards { get; }

    /// <summary>
    /// Occurs when a keyboard is connected.
    /// </summary>
    event Action<IKeyboardInputDevice>? KeyboardConnected;

    /// <summary>
    /// Occurs when a keyboard is disconnected.
    /// </summary>
    event Action<IKeyboardInputDevice>? KeyboardDisconnected;
}
