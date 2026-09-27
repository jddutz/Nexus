using System.Collections.ObjectModel;

namespace Nexus.Input;

/// <summary>
/// Adapts a Silk.NET input context to the engine input-device contract.
/// </summary>
public sealed class InputAdapter : IInputAdapter, IDisposable
{
    private readonly IInputContext _inputContext;
    private readonly Dictionary<int, Devices.Keyboard> _keyboardsByIndex = [];
    private readonly Dictionary<int, Devices.Mouse> _miceByIndex = [];
    private readonly List<IKeyboardInputDevice> _keyboards = [];
    private readonly List<IMouseInputDevice> _mice = [];
    private readonly ReadOnlyCollection<IKeyboardInputDevice> _keyboardView;
    private readonly ReadOnlyCollection<IMouseInputDevice> _mouseView;
    private bool _disposed;

    /// <summary>
    /// Gets the keyboards currently exposed by this adapter.
    /// </summary>
    public IReadOnlyCollection<IKeyboardInputDevice> Keyboards => _keyboardView;

    /// <summary>
    /// Gets the mice currently exposed by this adapter.
    /// </summary>
    public IReadOnlyCollection<IMouseInputDevice> Mice => _mouseView;

    /// <inheritdoc />
    public event Action<IKeyboardInputDevice>? KeyboardConnected;

    /// <inheritdoc />
    public event Action<IKeyboardInputDevice>? KeyboardDisconnected;

    /// <inheritdoc />
    public event Action<IMouseInputDevice>? MouseConnected;

    /// <inheritdoc />
    public event Action<IMouseInputDevice>? MouseDisconnected;

    /// <summary>
    /// Initializes an adapter for the specified Silk.NET input context.
    /// </summary>
    /// <param name="inputContext">The input context to observe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inputContext"/> is <see langword="null"/>.</exception>
    public InputAdapter(IInputContext inputContext)
    {
        ArgumentNullException.ThrowIfNull(inputContext);

        _inputContext = inputContext;
        _keyboardView = _keyboards.AsReadOnly();
        _mouseView = _mice.AsReadOnly();
        _inputContext.ConnectionChanged += OnConnectionChanged;

        foreach (var keyboard in _inputContext.Keyboards)
            AddKeyboard(keyboard, notify: false);

        foreach (var mouse in _inputContext.Mice)
            AddMouse(mouse, notify: false);
    }

    /// <summary>
    /// Releases the input-context event subscription and disconnects all exposed keyboards.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _inputContext.ConnectionChanged -= OnConnectionChanged;

        foreach (var index in _keyboardsByIndex.Keys.ToArray())
            RemoveKeyboard(index, notify: true);

        foreach (var index in _miceByIndex.Keys.ToArray())
            RemoveMouse(index, notify: true);
    }

    /// <summary>
    /// Updates the exposed keyboard collection when a device connection changes.
    /// </summary>
    /// <param name="device">The device whose connection state changed.</param>
    /// <param name="isConnected">Whether the device is connected.</param>
    private void OnConnectionChanged(Silk.NET.Input.IInputDevice device, bool isConnected)
    {
        if (_disposed)
            return;

        if (device is Silk.NET.Input.IKeyboard keyboard)
        {
            if (isConnected)
                AddKeyboard(keyboard, notify: true);
            else
                RemoveKeyboard(keyboard.Index, notify: true);
        }
        else if (device is Silk.NET.Input.IMouse mouse)
        {
            if (isConnected)
                AddMouse(mouse, notify: true);
            else
                RemoveMouse(mouse.Index, notify: true);
        }
    }

    /// <summary>
    /// Adds a keyboard to the exposed collection if it is not already registered.
    /// </summary>
    /// <param name="keyboard">The Silk.NET keyboard to add.</param>
    /// <param name="notify">Whether to raise the connection event.</param>
    private void AddKeyboard(Silk.NET.Input.IKeyboard keyboard, bool notify)
    {
        if (keyboard.Index < 0 || _keyboardsByIndex.ContainsKey(keyboard.Index))
            return;

        var adaptedKeyboard = new Devices.Keyboard(keyboard);
        _keyboardsByIndex.Add(keyboard.Index, adaptedKeyboard);
        _keyboards.Add(adaptedKeyboard);

        if (notify)
            KeyboardConnected?.Invoke(adaptedKeyboard);
    }

    /// <summary>
    /// Removes a keyboard from the exposed collection if it is registered.
    /// </summary>
    /// <param name="index">The Silk.NET device index of the keyboard to remove.</param>
    /// <param name="notify">Whether to raise the disconnection event.</param>
    private void RemoveKeyboard(int index, bool notify)
    {
        if (!_keyboardsByIndex.Remove(index, out var keyboard))
            return;

        _keyboards.Remove(keyboard);
        keyboard.Dispose();

        if (notify)
            KeyboardDisconnected?.Invoke(keyboard);
    }

    /// <summary>
    /// Adds a mouse to the exposed collection if it is not already registered.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse to add.</param>
    /// <param name="notify">Whether to raise the connection event.</param>
    private void AddMouse(Silk.NET.Input.IMouse mouse, bool notify)
    {
        if (mouse.Index < 0 || _miceByIndex.ContainsKey(mouse.Index))
            return;

        var adaptedMouse = new Devices.Mouse(mouse);
        _miceByIndex.Add(mouse.Index, adaptedMouse);
        _mice.Add(adaptedMouse);

        if (notify)
            MouseConnected?.Invoke(adaptedMouse);
    }

    /// <summary>
    /// Removes a mouse from the exposed collection if it is registered.
    /// </summary>
    /// <param name="index">The Silk.NET device index of the mouse to remove.</param>
    /// <param name="notify">Whether to raise the disconnection event.</param>
    private void RemoveMouse(int index, bool notify)
    {
        if (!_miceByIndex.Remove(index, out var mouse))
            return;

        _mice.Remove(mouse);
        mouse.Dispose();

        if (notify)
            MouseDisconnected?.Invoke(mouse);
    }
}
