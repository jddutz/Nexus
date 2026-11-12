namespace Nexus.Input;

using System.Collections.ObjectModel;
using Nexus.Core.Performance;
using Nexus.Input.Events;
using Silk.NET.Windowing;

/// <summary>
/// Provides the default input system implementation.
/// </summary>
public sealed class InputSystem : IInputSystem, IDisposable
{
    private readonly IEventHub _eventHub;
    private readonly IInputAdapter? _inputAdapter;
    private readonly IWindow? _window;
    private readonly IPerformanceTelemetry? _telemetry;
    private readonly KeyboardInputState _keyboard = new();
    private readonly MouseInputState _mouse = new();
    private readonly Dictionary<InputDeviceId, IKeyboardInputDevice> _keyboards = [];
    private readonly Dictionary<InputDeviceId, IMouseInputDevice> _mice = [];
    private readonly Dictionary<InputDeviceId, IController> _controllers = [];
    private readonly List<IController> _controllerList = [];
    private readonly ReadOnlyCollection<IController> _controllerView;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Gets aggregate state for all registered keyboards.
    /// </summary>
    public IKeyboardInputState Keyboard => _keyboard;

    /// <summary>
    /// Gets aggregate state for all registered mice.
    /// </summary>
    public IMouseInputState Mouse => _mouse;

    /// <summary>Gets the currently registered controllers in connection order.</summary>
    public IReadOnlyCollection<IController> Controllers => _controllerView;

    /// <inheritdoc />
    public event Action<KeyboardConnectedEvent>? KeyboardConnected;

    /// <inheritdoc />
    public event Action<KeyboardDisconnectedEvent>? KeyboardDisconnected;

    /// <inheritdoc />
    public event Action<KeyPressedEvent>? KeyPressed;

    /// <inheritdoc />
    public event Action<KeyReleasedEvent>? KeyReleased;

    /// <inheritdoc />
    public event Action<MouseConnectedEvent>? MouseConnected;

    /// <inheritdoc />
    public event Action<MouseDisconnectedEvent>? MouseDisconnected;

    /// <inheritdoc />
    public event Action<MouseMovedEvent>? MouseMoved;

    /// <inheritdoc />
    public event Action<MouseButtonPressedEvent>? MouseButtonPressed;

    /// <inheritdoc />
    public event Action<MouseButtonReleasedEvent>? MouseButtonReleased;

    /// <inheritdoc />
    public event Action<MouseWheelEvent>? MouseWheel;

    /// <inheritdoc />
    public event Action<MouseCanceledEvent>? MouseCanceled;

    /// <inheritdoc />
    public event Action<TouchStartedEvent>? TouchStarted;

    /// <inheritdoc />
    public event Action<TouchMovedEvent>? TouchMoved;

    /// <inheritdoc />
    public event Action<TouchEndedEvent>? TouchEnded;

    /// <inheritdoc />
    public event Action<ControllerConnectedEvent>? ControllerConnected;

    /// <inheritdoc />
    public event Action<ControllerDisconnectedEvent>? ControllerDisconnected;

    /// <inheritdoc />
    public event Action<ControllerButtonPressedEvent>? ControllerButtonPressed;

    /// <inheritdoc />
    public event Action<ControllerButtonReleasedEvent>? ControllerButtonReleased;

    /// <inheritdoc />
    public event Action<ControllerAnalogChangedEvent>? ControllerAnalogChanged;

    /// <summary>
    /// Initializes the input system with an event hub and an optional input adapter.
    /// </summary>
    /// <param name="eventHub">The global event hub.</param>
    /// <param name="inputAdapter">The adapter that reports keyboard connections and key transitions.</param>
    /// <param name="window">The optional window used for focus notifications.</param>
    /// <param name="telemetry">The optional performance telemetry sink.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventHub"/> is <see langword="null"/>.</exception>
    public InputSystem(
        IEventHub eventHub,
        IInputAdapter? inputAdapter = null,
        IWindow? window = null,
        IPerformanceTelemetry? telemetry = null
    )
    {
        ArgumentNullException.ThrowIfNull(eventHub);

        _eventHub = eventHub;
        _inputAdapter = inputAdapter;
        _window = window;
        _telemetry = telemetry;
        _controllerView = _controllerList.AsReadOnly();
    }

    /// <summary>Looks up a connected controller by its connection-specific identifier.</summary>
    /// <param name="id">The controller identifier.</param>
    /// <param name="controller">The matching controller, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the controller is registered.</returns>
    public bool TryGetController(InputDeviceId id, out IController? controller) =>
        _controllers.TryGetValue(id, out controller);

    /// <summary>
    /// Registers for keyboard changes and publishes currently connected keyboards.
    /// </summary>
    public void Initialize()
    {
        using var timing = new LoadPerformanceScope(
            _telemetry,
            "startup.system.initialize",
            "input"
        );
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
            return;

        _initialized = true;
        if (_window is not null)
            _window.FocusChanged += OnWindowFocusChanged;

        if (_inputAdapter is null)
            return;

        _inputAdapter.KeyboardConnected += OnKeyboardConnected;
        _inputAdapter.KeyboardDisconnected += OnKeyboardDisconnected;
        _inputAdapter.MouseConnected += OnMouseConnected;
        _inputAdapter.MouseDisconnected += OnMouseDisconnected;
        _inputAdapter.ControllerConnected += OnControllerConnected;
        _inputAdapter.ControllerDisconnected += OnControllerDisconnected;

        foreach (var keyboard in _inputAdapter.Keyboards)
            RegisterKeyboard(keyboard);

        foreach (var mouse in _inputAdapter.Mice)
            RegisterMouse(mouse);

        foreach (var controller in _inputAdapter.Controllers)
            RegisterController(controller);
    }

    /// <inheritdoc />
    public void Update(double deltaTime) => _inputAdapter?.Update(deltaTime);

    /// <inheritdoc />
    public void ReportTouchStarted(TouchStartedEvent inputEvent)
    {
        ValidateTouchReport(inputEvent);
        PublishInputEvent(inputEvent, TouchStarted);
    }

    /// <inheritdoc />
    public void ReportTouchMoved(TouchMovedEvent inputEvent)
    {
        ValidateTouchReport(inputEvent);
        PublishInputEvent(inputEvent, TouchMoved);
    }

    /// <inheritdoc />
    public void ReportTouchEnded(TouchEndedEvent inputEvent)
    {
        ValidateTouchReport(inputEvent);
        PublishInputEvent(inputEvent, TouchEnded);
    }

    /// <summary>Rejects touch reporting outside the input lifecycle or with a null payload.</summary>
    /// <param name="inputEvent">The touch event to report.</param>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="InvalidOperationException">The input system has not been initialized.</exception>
    /// <exception cref="ObjectDisposedException">The input system has been disposed.</exception>
    private void ValidateTouchReport(IEvent inputEvent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(inputEvent);
        if (!_initialized)
            throw new InvalidOperationException("Initialize the input system before reporting touch input.");
    }

    /// <summary>
    /// Releases adapter event subscriptions without taking ownership of the adapter.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_window is not null && _initialized)
            _window.FocusChanged -= OnWindowFocusChanged;

        if (_inputAdapter is not null && _initialized)
        {
            _inputAdapter.KeyboardConnected -= OnKeyboardConnected;
            _inputAdapter.KeyboardDisconnected -= OnKeyboardDisconnected;
            _inputAdapter.MouseConnected -= OnMouseConnected;
            _inputAdapter.MouseDisconnected -= OnMouseDisconnected;
            _inputAdapter.ControllerConnected -= OnControllerConnected;
            _inputAdapter.ControllerDisconnected -= OnControllerDisconnected;
        }

        foreach (var keyboard in _keyboards.Values)
        {
            UnsubscribeFromKeyboard(keyboard);
            _keyboard.Unregister(keyboard);
        }

        _keyboards.Clear();

        foreach (var mouse in _mice.Values)
            UnsubscribeFromMouse(mouse);

        foreach (var mouse in _mouse.Mice.ToArray())
            _mouse.Unregister(mouse);

        _mice.Clear();

        foreach (var controller in _controllers.Values)
            UnsubscribeFromController(controller);
        _controllers.Clear();
        _controllerList.Clear();
    }

    /// <summary>
    /// Registers a connected keyboard and publishes its connection event once.
    /// </summary>
    /// <param name="keyboard">The keyboard that connected.</param>
    private void OnKeyboardConnected(IKeyboardInputDevice keyboard) => RegisterKeyboard(keyboard);

    /// <summary>
    /// Unregisters a disconnected keyboard and publishes its disconnection event.
    /// </summary>
    /// <param name="keyboard">The keyboard that disconnected.</param>
    private void OnKeyboardDisconnected(IKeyboardInputDevice keyboard)
    {
        if (!_keyboards.Remove(keyboard.Id, out var registeredKeyboard))
            return;

        UnsubscribeFromKeyboard(registeredKeyboard);
        _keyboard.Unregister(registeredKeyboard);
        PublishInputEvent(new KeyboardDisconnectedEvent(registeredKeyboard), KeyboardDisconnected);
    }

    /// <summary>
    /// Adds a keyboard to aggregate state and subscribes to its key transitions.
    /// </summary>
    /// <param name="keyboard">The keyboard to register.</param>
    private void RegisterKeyboard(IKeyboardInputDevice keyboard)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        if (!_keyboards.TryAdd(keyboard.Id, keyboard))
            return;

        _keyboard.Register(keyboard);
        keyboard.KeyPressed += OnKeyPressed;
        keyboard.KeyReleased += OnKeyReleased;
        PublishInputEvent(new KeyboardConnectedEvent(keyboard), KeyboardConnected);
    }

    /// <summary>
    /// Publishes a key press reported by a registered keyboard.
    /// </summary>
    /// <param name="keyboard">The keyboard reporting the key.</param>
    /// <param name="key">The key that was pressed.</param>
    private void OnKeyPressed(IKeyboardInputDevice keyboard, KeyEnum key)
    {
        if (_keyboards.ContainsKey(keyboard.Id))
            PublishInputEvent(new KeyPressedEvent(keyboard, key), KeyPressed);
    }

    /// <summary>
    /// Publishes a key release reported by a registered keyboard.
    /// </summary>
    /// <param name="keyboard">The keyboard reporting the key.</param>
    /// <param name="key">The key that was released.</param>
    private void OnKeyReleased(IKeyboardInputDevice keyboard, KeyEnum key)
    {
        if (_keyboards.ContainsKey(keyboard.Id))
            PublishInputEvent(new KeyReleasedEvent(keyboard, key), KeyReleased);
    }

    /// <summary>
    /// Removes this system's callbacks from a keyboard.
    /// </summary>
    /// <param name="keyboard">The keyboard to unsubscribe from.</param>
    private void UnsubscribeFromKeyboard(IKeyboardInputDevice keyboard)
    {
        keyboard.KeyPressed -= OnKeyPressed;
        keyboard.KeyReleased -= OnKeyReleased;
    }

    /// <summary>
    /// Registers a connected mouse and publishes its connection event once.
    /// </summary>
    /// <param name="mouse">The mouse that connected.</param>
    private void OnMouseConnected(IMouseInputDevice mouse) => RegisterMouse(mouse);

    /// <summary>
    /// Unregisters a disconnected mouse and publishes its disconnection event.
    /// </summary>
    /// <param name="mouse">The mouse that disconnected.</param>
    private void OnMouseDisconnected(IMouseInputDevice mouse)
    {
        if (!_mice.Remove(mouse.Id, out var registeredMouse))
            return;

        UnsubscribeFromMouse(registeredMouse);
        _mouse.Unregister(registeredMouse);
        PublishInputEvent(
            new MouseDisconnectedEvent(registeredMouse, registeredMouse.Position),
            MouseDisconnected
        );
    }

    /// <summary>
    /// Adds a mouse to aggregate state and subscribes to its transitions.
    /// </summary>
    /// <param name="mouse">The mouse to register.</param>
    private void RegisterMouse(IMouseInputDevice mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        if (!_mice.TryAdd(mouse.Id, mouse))
            return;

        _mouse.Register(mouse);
        mouse.Moved += OnMouseMoved;
        mouse.ButtonPressed += OnMouseButtonPressed;
        mouse.ButtonReleased += OnMouseButtonReleased;
        mouse.WheelMoved += OnMouseWheelMoved;
        PublishInputEvent(new MouseConnectedEvent(mouse, mouse.Position), MouseConnected);
    }

    /// <summary>
    /// Publishes a position change reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting movement.</param>
    /// <param name="position">The new mouse position.</param>
    private void OnMouseMoved(IMouseInputDevice mouse, Vector2D<float> position)
    {
        if (_mice.ContainsKey(mouse.Id))
            PublishInputEvent(new MouseMovedEvent(mouse, position), MouseMoved);
    }

    /// <summary>
    /// Publishes a button press reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the button.</param>
    /// <param name="button">The button that was pressed.</param>
    private void OnMouseButtonPressed(IMouseInputDevice mouse, MouseButtonEnum button)
    {
        if (_mice.ContainsKey(mouse.Id))
            PublishInputEvent(
                new MouseButtonPressedEvent(mouse, button, mouse.Position),
                MouseButtonPressed
            );
    }

    /// <summary>
    /// Publishes a button release reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the button.</param>
    /// <param name="button">The button that was released.</param>
    private void OnMouseButtonReleased(IMouseInputDevice mouse, MouseButtonEnum button)
    {
        if (_mice.ContainsKey(mouse.Id))
            PublishInputEvent(
                new MouseButtonReleasedEvent(mouse, button, mouse.Position),
                MouseButtonReleased
            );
    }

    /// <summary>
    /// Publishes a wheel movement reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the wheel movement.</param>
    /// <param name="delta">The scroll delta.</param>
    private void OnMouseWheelMoved(IMouseInputDevice mouse, Vector2D<float> delta)
    {
        if (_mice.ContainsKey(mouse.Id))
            PublishInputEvent(new MouseWheelEvent(mouse, delta, mouse.Position), MouseWheel);
    }

    /// <summary>Cancels current mouse interactions when the application window loses focus.</summary>
    /// <param name="isFocused">Whether the application window currently has focus.</param>
    private void OnWindowFocusChanged(bool isFocused)
    {
        if (isFocused)
            return;

        foreach (var mouse in _mice.Values.ToArray())
            PublishInputEvent(new MouseCanceledEvent(mouse, mouse.Position), MouseCanceled);
    }

    /// <summary>
    /// Removes this system's callbacks from a mouse.
    /// </summary>
    /// <param name="mouse">The mouse to unsubscribe from.</param>
    private void UnsubscribeFromMouse(IMouseInputDevice mouse)
    {
        mouse.Moved -= OnMouseMoved;
        mouse.ButtonPressed -= OnMouseButtonPressed;
        mouse.ButtonReleased -= OnMouseButtonReleased;
        mouse.WheelMoved -= OnMouseWheelMoved;
    }

    /// <summary>Registers a connected controller and publishes its connection event once.</summary>
    /// <param name="controller">The controller that connected.</param>
    private void OnControllerConnected(IController controller) => RegisterController(controller);

    /// <summary>Unregisters a disconnected controller and publishes its disconnection event.</summary>
    /// <param name="controller">The controller that disconnected.</param>
    private void OnControllerDisconnected(IController controller)
    {
        if (!_controllers.Remove(controller.Id, out var registeredController))
            return;

        UnsubscribeFromController(registeredController);
        _controllerList.Remove(registeredController);
        PublishInputEvent(
            new ControllerDisconnectedEvent(registeredController),
            ControllerDisconnected
        );
    }

    /// <summary>Adds a controller and subscribes to its logical control transitions.</summary>
    /// <param name="controller">The controller to register.</param>
    private void RegisterController(IController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (!_controllers.TryAdd(controller.Id, controller))
            return;

        _controllerList.Add(controller);
        controller.ButtonPressed += OnControllerButtonPressed;
        controller.ButtonReleased += OnControllerButtonReleased;
        controller.AnalogChanged += OnControllerAnalogChanged;
        PublishInputEvent(new ControllerConnectedEvent(controller), ControllerConnected);
    }

    /// <summary>Publishes a button-press event for a registered controller.</summary>
    /// <param name="controller">The controller reporting the transition.</param>
    /// <param name="button">The button that was pressed.</param>
    private void OnControllerButtonPressed(IController controller, IButtonInput button)
    {
        if (IsRegisteredController(controller))
            PublishInputEvent(
                new ControllerButtonPressedEvent(controller, button),
                ControllerButtonPressed
            );
    }

    /// <summary>Publishes a button-release event for a registered controller.</summary>
    /// <param name="controller">The controller reporting the transition.</param>
    /// <param name="button">The button that was released.</param>
    private void OnControllerButtonReleased(IController controller, IButtonInput button)
    {
        if (IsRegisteredController(controller))
            PublishInputEvent(
                new ControllerButtonReleasedEvent(controller, button),
                ControllerButtonReleased
            );
    }

    /// <summary>Publishes a captured analog position for a registered controller.</summary>
    /// <param name="controller">The controller reporting the change.</param>
    /// <param name="analogInput">The analog input that changed.</param>
    /// <param name="position">The normalized position captured at the transition.</param>
    private void OnControllerAnalogChanged(
        IController controller,
        IAnalogInput analogInput,
        Vector2D<float> position
    )
    {
        if (IsRegisteredController(controller))
            PublishInputEvent(
                new ControllerAnalogChangedEvent(controller, analogInput, position),
                ControllerAnalogChanged
            );
    }

    /// <summary>Queues an input event and synchronously notifies its direct subscribers.</summary>
    /// <typeparam name="TEvent">The type of input event.</typeparam>
    /// <param name="inputEvent">The event to queue and forward.</param>
    /// <param name="immediateHandler">The subscribers to notify immediately.</param>
    private void PublishInputEvent<TEvent>(TEvent inputEvent, Action<TEvent>? immediateHandler)
        where TEvent : IEvent
    {
        _eventHub.Publish(inputEvent);
        immediateHandler?.Invoke(inputEvent);
    }

    /// <summary>Removes this system's callbacks from a concrete controller wrapper.</summary>
    /// <param name="controller">The controller to unsubscribe.</param>
    private void UnsubscribeFromController(IController controller)
    {
        controller.ButtonPressed -= OnControllerButtonPressed;
        controller.ButtonReleased -= OnControllerButtonReleased;
        controller.AnalogChanged -= OnControllerAnalogChanged;
    }

    /// <summary>Determines whether a callback came from the currently registered controller instance.</summary>
    /// <param name="controller">The controller reporting the transition.</param>
    /// <returns><see langword="true"/> only for the registered instance.</returns>
    private bool IsRegisteredController(IController controller) =>
        _controllers.TryGetValue(controller.Id, out var registeredController)
        && ReferenceEquals(registeredController, controller);
}
