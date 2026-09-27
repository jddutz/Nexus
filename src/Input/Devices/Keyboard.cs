namespace Nexus.Input.Devices;

/// <summary>
/// Adapts a Silk.NET keyboard to the engine keyboard-device contract.
/// </summary>
public class Keyboard : IKeyboardInputDevice, IDisposable
{
    private readonly Silk.NET.Input.IKeyboard _keyboard;
    private readonly InputDeviceId _id;
    private bool _disposed;

    /// <summary>
    /// Initializes a keyboard adapter for the specified Silk.NET keyboard.
    /// </summary>
    /// <param name="keyboard">The Silk.NET keyboard to adapt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyboard"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The keyboard has a negative device index.</exception>
    public Keyboard(Silk.NET.Input.IKeyboard keyboard)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        if (keyboard.Index < 0)
            throw new ArgumentOutOfRangeException(nameof(keyboard), "The keyboard index must be non-negative.");

        _keyboard = keyboard;
        _id = new InputDeviceId((ulong)keyboard.Index + 1);
        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;
    }

    /// <summary>
    /// Gets the engine identifier derived from the Silk.NET device index.
    /// </summary>
    public InputDeviceId Id => _id;

    /// <summary>
    /// Gets the device name reported by Silk.NET.
    /// </summary>
    public string Name => _keyboard.Name;

    /// <summary>
    /// Gets whether the Silk.NET keyboard is currently connected.
    /// </summary>
    public bool IsConnected => _keyboard.IsConnected;

    /// <summary>
    /// Gets whether the specified key is currently pressed on this keyboard.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> if the key is pressed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="KeyEnum"/>.</exception>
    public bool IsKeyDown(KeyEnum key) => _keyboard.IsKeyPressed(key.ToSilkKey());

    /// <summary>
    /// Releases the native keyboard event subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _keyboard.KeyDown -= OnKeyDown;
        _keyboard.KeyUp -= OnKeyUp;
        _disposed = true;
    }

    /// <summary>
    /// Occurs when a key is pressed on this keyboard.
    /// </summary>
    public event Action<IKeyboardInputDevice, KeyEnum>? KeyPressed;

    /// <summary>
    /// Occurs when a key is released on this keyboard.
    /// </summary>
    public event Action<IKeyboardInputDevice, KeyEnum>? KeyReleased;

    /// <summary>
    /// Converts a Silk.NET key-down callback into the engine key event.
    /// </summary>
    /// <param name="keyboard">The Silk.NET keyboard reporting the key.</param>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    private void OnKeyDown(Silk.NET.Input.IKeyboard keyboard, SilkKey key, int modifiers) =>
        KeyPressed?.Invoke(this, key.ToKeyEnum());

    /// <summary>
    /// Converts a Silk.NET key-up callback into the engine key event.
    /// </summary>
    /// <param name="keyboard">The Silk.NET keyboard reporting the key.</param>
    /// <param name="key">The key that was released.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    private void OnKeyUp(Silk.NET.Input.IKeyboard keyboard, SilkKey key, int modifiers) =>
        KeyReleased?.Invoke(this, key.ToKeyEnum());
}
