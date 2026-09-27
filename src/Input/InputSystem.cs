namespace Nexus.Input;

/// <summary>
/// Provides the default input system implementation.
/// </summary>
public sealed class InputSystem : IInputSystem, IDisposable
{
    private readonly IEventHub _eventHub;
    private readonly IInputAdapter? _inputAdapter;
    private readonly KeyboardInputState _keyboard = new();
    private readonly Dictionary<InputDeviceId, IKeyboardInputDevice> _keyboards = [];
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Gets aggregate state for all registered keyboards.
    /// </summary>
    public IKeyboardInputState Keyboard => _keyboard;

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

        foreach (var keyboard in _inputAdapter.Keyboards)
            RegisterKeyboard(keyboard);
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
        if (_inputAdapter is not null && _initialized)
        {
            _inputAdapter.KeyboardConnected -= OnKeyboardConnected;
            _inputAdapter.KeyboardDisconnected -= OnKeyboardDisconnected;
        }

        foreach (var keyboard in _keyboards.Values)
        {
            UnsubscribeFromKeyboard(keyboard);
            _keyboard.Unregister(keyboard);
        }

        _keyboards.Clear();
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
}
