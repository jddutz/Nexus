namespace Nexus.Input.Devices;

/// <summary>
/// Adapts a Silk.NET mouse to the engine mouse-device contract.
/// </summary>
public sealed class Mouse : IMouseInputDevice, IDisposable
{
    private readonly IMouse _mouse;
    private readonly InputDeviceId _id;
    private bool _disposed;

    /// <summary>
    /// Initializes a mouse adapter for the specified Silk.NET mouse.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse to adapt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mouse"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The mouse has a negative device index.</exception>
    public Mouse(IMouse mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);

        if (mouse.Index < 0)
            throw new ArgumentOutOfRangeException(
                nameof(mouse),
                "The mouse index must be non-negative."
            );

        _mouse = mouse;
        _id = new InputDeviceId((ulong)mouse.Index + 1);
        _mouse.MouseMove += OnMouseMove;
        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;
        _mouse.Scroll += OnScroll;
    }

    /// <summary>
    /// Gets the engine identifier derived from the Silk.NET device index.
    /// </summary>
    public InputDeviceId Id => _id;

    /// <summary>
    /// Gets the device name reported by Silk.NET.
    /// </summary>
    public string Name => _mouse.Name;

    /// <summary>
    /// Gets whether the Silk.NET mouse is currently connected.
    /// </summary>
    public bool IsConnected => _mouse.IsConnected;

    /// <summary>
    /// Gets the current mouse position in window coordinates.
    /// </summary>
    public Vector2D<float> Position => ConvertPosition(_mouse.Position);

    /// <summary>
    /// Occurs when the mouse position changes.
    /// </summary>
    public event Action<IMouseInputDevice, Vector2D<float>>? Moved;

    /// <summary>
    /// Occurs when a mouse button is pressed.
    /// </summary>
    public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonPressed;

    /// <summary>
    /// Occurs when a mouse button is released.
    /// </summary>
    public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonReleased;

    /// <summary>
    /// Occurs when the mouse wheel moves.
    /// </summary>
    public event Action<IMouseInputDevice, Vector2D<float>>? WheelMoved;

    /// <summary>
    /// Gets whether the specified mouse button is currently down.
    /// </summary>
    /// <param name="button">The button to check.</param>
    /// <returns><see langword="true"/> if the button is down; otherwise, <see langword="false"/>.</returns>
    public bool IsButtonDown(MouseButtonEnum button) =>
        button switch
        {
            MouseButtonEnum.Left => _mouse.IsButtonPressed(MouseButton.Left),
            MouseButtonEnum.Right => _mouse.IsButtonPressed(MouseButton.Right),
            MouseButtonEnum.Middle => _mouse.IsButtonPressed(MouseButton.Middle),
            MouseButtonEnum.Button4 => _mouse.IsButtonPressed(MouseButton.Button4),
            MouseButtonEnum.Button5 => _mouse.IsButtonPressed(MouseButton.Button5),
            MouseButtonEnum.Button6 => _mouse.IsButtonPressed(MouseButton.Button6),
            MouseButtonEnum.Button7 => _mouse.IsButtonPressed(MouseButton.Button7),
            MouseButtonEnum.Button8 => _mouse.IsButtonPressed(MouseButton.Button8),
            _ => false,
        };

    /// <summary>
    /// Releases the native mouse event subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _mouse.MouseMove -= OnMouseMove;
        _mouse.MouseDown -= OnMouseDown;
        _mouse.MouseUp -= OnMouseUp;
        _mouse.Scroll -= OnScroll;
        _disposed = true;
    }

    /// <summary>
    /// Converts a Silk.NET mouse-move callback into the engine position event.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse reporting movement.</param>
    /// <param name="position">The new mouse position.</param>
    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position) =>
        Moved?.Invoke(this, ConvertPosition(position));

    /// <summary>
    /// Converts a Silk.NET button-down callback into the engine button event.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse reporting the button.</param>
    /// <param name="button">The button that was pressed.</param>
    private void OnMouseDown(IMouse mouse, MouseButton button) =>
        ButtonPressed?.Invoke(this, ConvertButton(button));

    /// <summary>
    /// Converts a Silk.NET button-up callback into the engine button event.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse reporting the button.</param>
    /// <param name="button">The button that was released.</param>
    private void OnMouseUp(IMouse mouse, MouseButton button) =>
        ButtonReleased?.Invoke(this, ConvertButton(button));

    /// <summary>
    /// Converts a Silk.NET scroll callback into the engine wheel event.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse reporting the scroll.</param>
    /// <param name="wheel">The scroll amount.</param>
    private void OnScroll(IMouse mouse, ScrollWheel wheel) =>
        WheelMoved?.Invoke(this, new Vector2D<float>((float)wheel.X, (float)wheel.Y));

    /// <summary>
    /// Converts a Silk.NET position into the engine's single-precision coordinate type.
    /// </summary>
    /// <param name="position">The Silk.NET position.</param>
    /// <returns>The converted position.</returns>
    private static Vector2D<float> ConvertPosition(System.Numerics.Vector2 position) =>
        new(position.X, position.Y);

    /// <summary>
    /// Converts a Silk.NET button identifier into the engine button identifier.
    /// </summary>
    /// <param name="button">The Silk.NET button.</param>
    /// <returns>The matching engine button, or <see cref="MouseButtonEnum.Unknown"/>.</returns>
    private static MouseButtonEnum ConvertButton(MouseButton button) =>
        button switch
        {
            MouseButton.Left => MouseButtonEnum.Left,
            MouseButton.Right => MouseButtonEnum.Right,
            MouseButton.Middle => MouseButtonEnum.Middle,
            MouseButton.Button4 => MouseButtonEnum.Button4,
            MouseButton.Button5 => MouseButtonEnum.Button5,
            MouseButton.Button6 => MouseButtonEnum.Button6,
            MouseButton.Button7 => MouseButtonEnum.Button7,
            MouseButton.Button8 => MouseButtonEnum.Button8,
            _ => MouseButtonEnum.Unknown,
        };
}
