namespace Nexus.Input;

public interface IInputAdapter
{
    /// <summary>Gets the controllers currently exposed by this adapter.</summary>
    IReadOnlyCollection<IController> Controllers { get; }

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

    /// <summary>Occurs when a controller connects.</summary>
    event Action<IController>? ControllerConnected;

    /// <summary>Occurs when a controller disconnects.</summary>
    event Action<IController>? ControllerDisconnected;

    /// <summary>Samples physical devices and reports observed state transitions.</summary>
    /// <param name="deltaTime">Elapsed time in seconds since the previous input update.</param>
    void Update(double deltaTime);
}
