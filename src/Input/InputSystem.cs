namespace Nexus.Input;

using Nexus.Input.Events;

/// <summary>
/// Provides the default input system implementation.
/// </summary>
public sealed class InputSystem : IInputSystem, IDisposable
{
    private readonly IEventHub _eventHub;
    private readonly IInputAdapter? _inputAdapter;
    private readonly KeyboardInputState _keyboard = new();
    private readonly MouseInputState _mouse = new();
    private readonly Dictionary<InputDeviceId, IKeyboardInputDevice> _keyboards = [];
    private readonly Dictionary<InputDeviceId, IMouseInputDevice> _mice = [];
    private SceneInputMap? _currentMap;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the scene input map that receives dispatched keyboard events.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The input system has been disposed.</exception>
    public SceneInputMap? CurrentMap
    {
        get => _currentMap;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ReferenceEquals(_currentMap, value))
                return;

            if (_currentMap is not null)
                _eventHub.Unregister(_currentMap);

            _currentMap = value;
            if (_currentMap is not null)
                _eventHub.Register(_currentMap);
        }
    }

    /// <summary>
    /// Gets aggregate state for all registered keyboards.
    /// </summary>
    public IKeyboardInputState Keyboard => _keyboard;

    /// <summary>
    /// Gets aggregate state for all registered mice.
    /// </summary>
    public IMouseInputState Mouse => _mouse;

    /// <summary>
    /// Initializes the input system with an event hub and an optional input adapter.
    /// </summary>
    /// <param name="eventHub">The global event hub.</param>
    /// <param name="inputAdapter">The adapter that reports keyboard connections and key transitions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventHub"/> is <see langword="null"/>.</exception>
    public InputSystem(IEventHub eventHub, IInputAdapter? inputAdapter = null)
    {
        ArgumentNullException.ThrowIfNull(eventHub);

        _eventHub = eventHub;
        _inputAdapter = inputAdapter;
    }

    /// <summary>
    /// Registers for keyboard changes and publishes currently connected keyboards.
    /// </summary>
    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
            return;

        _initialized = true;
        if (_inputAdapter is null)
            return;

        _inputAdapter.KeyboardConnected += OnKeyboardConnected;
        _inputAdapter.KeyboardDisconnected += OnKeyboardDisconnected;
        _inputAdapter.MouseConnected += OnMouseConnected;
        _inputAdapter.MouseDisconnected += OnMouseDisconnected;

        foreach (var keyboard in _inputAdapter.Keyboards)
            RegisterKeyboard(keyboard);

        foreach (var mouse in _inputAdapter.Mice)
            RegisterMouse(mouse);
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    /// <summary>
    /// Releases adapter event subscriptions without taking ownership of the adapter.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_currentMap is not null)
        {
            _eventHub.Unregister(_currentMap);
            _currentMap = null;
        }

        if (_inputAdapter is not null && _initialized)
        {
            _inputAdapter.KeyboardConnected -= OnKeyboardConnected;
            _inputAdapter.KeyboardDisconnected -= OnKeyboardDisconnected;
            _inputAdapter.MouseConnected -= OnMouseConnected;
            _inputAdapter.MouseDisconnected -= OnMouseDisconnected;
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
        _eventHub.Publish(new MouseDisconnectedEvent(registeredMouse));
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
        _eventHub.Publish(new MouseConnectedEvent(mouse));
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
            _eventHub.Publish(new MouseButtonPressedEvent(mouse, button));
    }

    /// <summary>
    /// Publishes a button release reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the button.</param>
    /// <param name="button">The button that was released.</param>
    private void OnMouseButtonReleased(IMouseInputDevice mouse, MouseButtonEnum button)
    {
        if (_mice.ContainsKey(mouse.Id))
            _eventHub.Publish(new MouseButtonReleasedEvent(mouse, button));
    }

    /// <summary>
    /// Publishes a wheel movement reported by a registered mouse.
    /// </summary>
    /// <param name="mouse">The mouse reporting the wheel movement.</param>
    /// <param name="delta">The scroll delta.</param>
    private void OnMouseWheelMoved(IMouseInputDevice mouse, Vector2D<float> delta)
    {
        if (_mice.ContainsKey(mouse.Id))
            _eventHub.Publish(new MouseWheelEvent(mouse, delta));
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
}
