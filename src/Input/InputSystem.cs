namespace Nexus.Input;

using System.Collections.ObjectModel;
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

    /// <summary>
    /// Initializes the input system with an event hub and an optional input adapter.
    /// </summary>
    /// <param name="eventHub">The global event hub.</param>
    /// <param name="inputAdapter">The adapter that reports keyboard connections and key transitions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventHub"/> is <see langword="null"/>.</exception>
    public InputSystem(
        IEventHub eventHub,
        IInputAdapter? inputAdapter = null,
        IWindow? window = null
    )
    {
        ArgumentNullException.ThrowIfNull(eventHub);

        _eventHub = eventHub;
        _inputAdapter = inputAdapter;
        _window = window;
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
        _eventHub.Publish(new KeyboardDisconnectedEvent(registeredKeyboard));
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
        _eventHub.Publish(new KeyboardConnectedEvent(keyboard));
    }

    /// <summary>
    /// Publishes a key press reported by a registered keyboard.
    /// </summary>
    /// <param name="keyboard">The keyboard reporting the key.</param>
    /// <param name="key">The key that was pressed.</param>
    private void OnKeyPressed(IKeyboardInputDevice keyboard, KeyEnum key)
    {
        if (_keyboards.ContainsKey(keyboard.Id))
            _eventHub.Publish(new KeyPressedEvent(keyboard, key));
    }

    /// <summary>
    /// Publishes a key release reported by a registered keyboard.
    /// </summary>
    /// <param name="keyboard">The keyboard reporting the key.</param>
    /// <param name="key">The key that was released.</param>
    private void OnKeyReleased(IKeyboardInputDevice keyboard, KeyEnum key)
    {
        if (_keyboards.ContainsKey(keyboard.Id))
            _eventHub.Publish(new KeyReleasedEvent(keyboard, key));
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
        _eventHub.Publish(new MouseDisconnectedEvent(registeredMouse, registeredMouse.Position));
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
        _eventHub.Publish(new MouseConnectedEvent(mouse, mouse.Position));
    }

    /// <summary>
    /// Publishes a position change reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting movement.</param>
    /// <param name="position">The new mouse position.</param>
    private void OnMouseMoved(IMouseInputDevice mouse, Vector2D<float> position)
    {
        if (_mice.ContainsKey(mouse.Id))
            _eventHub.Publish(new MouseMovedEvent(mouse, position));
    }

    /// <summary>
    /// Publishes a button press reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the button.</param>
    /// <param name="button">The button that was pressed.</param>
    private void OnMouseButtonPressed(IMouseInputDevice mouse, MouseButtonEnum button)
    {
        if (_mice.ContainsKey(mouse.Id))
            _eventHub.Publish(new MouseButtonPressedEvent(mouse, button, mouse.Position));
    }

    /// <summary>
    /// Publishes a button release reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the button.</param>
    /// <param name="button">The button that was released.</param>
    private void OnMouseButtonReleased(IMouseInputDevice mouse, MouseButtonEnum button)
    {
        if (_mice.ContainsKey(mouse.Id))
            _eventHub.Publish(new MouseButtonReleasedEvent(mouse, button, mouse.Position));
    }

    /// <summary>
    /// Publishes a wheel movement reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the wheel movement.</param>
    /// <param name="delta">The scroll delta.</param>
    private void OnMouseWheelMoved(IMouseInputDevice mouse, Vector2D<float> delta)
    {
        if (_mice.ContainsKey(mouse.Id))
            _eventHub.Publish(new MouseWheelEvent(mouse, delta, mouse.Position));
    }

    /// <summary>Cancels current mouse interactions when the application window loses focus.</summary>
    /// <param name="isFocused">Whether the application window currently has focus.</param>
    private void OnWindowFocusChanged(bool isFocused)
    {
        if (isFocused)
            return;

        foreach (var mouse in _mice.Values)
            _eventHub.Publish(new MouseCanceledEvent(mouse, mouse.Position));
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
        _eventHub.Publish(new ControllerDisconnectedEvent(registeredController));
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
        _eventHub.Publish(new ControllerConnectedEvent(controller));
    }

    /// <summary>Publishes a button-press event for a registered controller.</summary>
    /// <param name="controller">The controller reporting the transition.</param>
    /// <param name="button">The button that was pressed.</param>
    private void OnControllerButtonPressed(IController controller, IButtonInput button)
    {
        if (IsRegisteredController(controller))
            _eventHub.Publish(new ControllerButtonPressedEvent(controller, button));
    }

    /// <summary>Publishes a button-release event for a registered controller.</summary>
    /// <param name="controller">The controller reporting the transition.</param>
    /// <param name="button">The button that was released.</param>
    private void OnControllerButtonReleased(IController controller, IButtonInput button)
    {
        if (IsRegisteredController(controller))
            _eventHub.Publish(new ControllerButtonReleasedEvent(controller, button));
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
            _eventHub.Publish(new ControllerAnalogChangedEvent(controller, analogInput, position));
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
