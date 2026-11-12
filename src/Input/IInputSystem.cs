namespace Nexus.Input;

using Nexus.Input.Events;

public interface IInputSystem
{
    /// <summary>
    /// Aggregated keyboard state. Reads the overall state from all currently connected keyboards.
    /// </summary>
    IKeyboardInputState Keyboard { get; }

    /// <summary>
    /// Aggregated mouse state with device-specific access by identifier.
    /// </summary>
    IMouseInputState Mouse { get; }

    /// <summary>Gets all currently registered controllers.</summary>
    IReadOnlyCollection<IController> Controllers { get; }

    /// <inheritdoc cref="KeyboardConnectedEvent"/>
    event Action<KeyboardConnectedEvent>? KeyboardConnected;

    /// <inheritdoc cref="KeyboardDisconnectedEvent"/>
    event Action<KeyboardDisconnectedEvent>? KeyboardDisconnected;

    /// <inheritdoc cref="KeyPressedEvent"/>
    event Action<KeyPressedEvent>? KeyPressed;

    /// <inheritdoc cref="KeyReleasedEvent"/>
    event Action<KeyReleasedEvent>? KeyReleased;

    /// <inheritdoc cref="MouseConnectedEvent"/>
    event Action<MouseConnectedEvent>? MouseConnected;

    /// <inheritdoc cref="MouseDisconnectedEvent"/>
    event Action<MouseDisconnectedEvent>? MouseDisconnected;

    /// <inheritdoc cref="MouseMovedEvent"/>
    event Action<MouseMovedEvent>? MouseMoved;

    /// <inheritdoc cref="MouseButtonPressedEvent"/>
    event Action<MouseButtonPressedEvent>? MouseButtonPressed;

    /// <inheritdoc cref="MouseButtonReleasedEvent"/>
    event Action<MouseButtonReleasedEvent>? MouseButtonReleased;

    /// <inheritdoc cref="MouseWheelEvent"/>
    event Action<MouseWheelEvent>? MouseWheel;

    /// <inheritdoc cref="MouseCanceledEvent"/>
    event Action<MouseCanceledEvent>? MouseCanceled;

    /// <summary>Occurs synchronously when a platform reports a touch starting.</summary>
    event Action<TouchStartedEvent>? TouchStarted;

    /// <summary>Occurs synchronously when a platform reports a touch moving.</summary>
    event Action<TouchMovedEvent>? TouchMoved;

    /// <summary>Occurs synchronously when a platform reports a touch ending.</summary>
    event Action<TouchEndedEvent>? TouchEnded;

    /// <inheritdoc cref="ControllerConnectedEvent"/>
    event Action<ControllerConnectedEvent>? ControllerConnected;

    /// <inheritdoc cref="ControllerDisconnectedEvent"/>
    event Action<ControllerDisconnectedEvent>? ControllerDisconnected;

    /// <inheritdoc cref="ControllerButtonPressedEvent"/>
    event Action<ControllerButtonPressedEvent>? ControllerButtonPressed;

    /// <inheritdoc cref="ControllerButtonReleasedEvent"/>
    event Action<ControllerButtonReleasedEvent>? ControllerButtonReleased;

    /// <inheritdoc cref="ControllerAnalogChangedEvent"/>
    event Action<ControllerAnalogChangedEvent>? ControllerAnalogChanged;

    /// <summary>Looks up a currently registered controller by its connection-specific identifier.</summary>
    /// <param name="id">The controller identifier.</param>
    /// <param name="controller">The matching controller, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the controller is registered.</returns>
    bool TryGetController(InputDeviceId id, out IController? controller);

    /// <summary>Immediately reports a platform touch start and queues it for EventHub dispatch.</summary>
    /// <param name="inputEvent">The touch start, with its captured screen position.</param>
    /// <remarks>Subscribers execute synchronously on the reporting thread. Call from the runtime input thread.</remarks>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="InvalidOperationException">The input system has not been initialized.</exception>
    /// <exception cref="ObjectDisposedException">The input system has been disposed.</exception>
    void ReportTouchStarted(TouchStartedEvent inputEvent);

    /// <summary>Immediately reports a platform touch movement and queues it for EventHub dispatch.</summary>
    /// <param name="inputEvent">The touch movement, with its captured screen position.</param>
    /// <remarks>Subscribers execute synchronously on the reporting thread. Call from the runtime input thread.</remarks>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="InvalidOperationException">The input system has not been initialized.</exception>
    /// <exception cref="ObjectDisposedException">The input system has been disposed.</exception>
    void ReportTouchMoved(TouchMovedEvent inputEvent);

    /// <summary>Immediately reports a platform touch end and queues it for EventHub dispatch.</summary>
    /// <param name="inputEvent">The touch end, with its captured screen position.</param>
    /// <remarks>Subscribers execute synchronously on the reporting thread. Call from the runtime input thread.</remarks>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="InvalidOperationException">The input system has not been initialized.</exception>
    /// <exception cref="ObjectDisposedException">The input system has been disposed.</exception>
    void ReportTouchEnded(TouchEndedEvent inputEvent);

    /// <summary>
    /// Initializes the input system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the input system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);
}
