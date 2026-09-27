using System.Collections.ObjectModel;

namespace Nexus.Input;

/// <summary>
/// Tracks mouse devices and exposes their common single-mouse state.
/// </summary>
public sealed class MouseInputState : IMouseInputState
{
    private readonly Dictionary<InputDeviceId, IMouseInputDevice> _mice = [];
    private readonly List<IMouseInputDevice> _mouseList = [];
    private readonly ReadOnlyCollection<IMouseInputDevice> _mouseView;

    /// <summary>
    /// Initializes an empty aggregate mouse state.
    /// </summary>
    public MouseInputState()
    {
        _mouseView = _mouseList.AsReadOnly();
    }

    /// <summary>
    /// Gets the registered mouse for the specified device identifier.
    /// </summary>
    /// <param name="id">The identifier of the mouse to retrieve.</param>
    /// <returns>The registered mouse with the specified identifier.</returns>
    /// <exception cref="KeyNotFoundException">No mouse is registered with the specified identifier.</exception>
    public IMouseInputDevice this[InputDeviceId id] => _mice[id];

    /// <summary>
    /// Gets the connected mice currently registered with this input system.
    /// </summary>
    public IReadOnlyCollection<IMouseInputDevice> Mice => _mouseView;

    /// <summary>
    /// Gets the current position of the first connected mouse, or zero if none is connected.
    /// </summary>
    public Vector2D<float> Position =>
        _mouseList.FirstOrDefault(mouse => mouse.IsConnected)?.Position ?? Vector2D<float>.Zero;

    /// <summary>
    /// Registers a mouse device with this aggregate state.
    /// </summary>
    /// <param name="mouse">The mouse device to register.</param>
    public void Register(IMouseInputDevice mouse)
    {
        if (_mice.TryAdd(mouse.Id, mouse))
            _mouseList.Add(mouse);
    }

    /// <summary>
    /// Unregisters a mouse device from this aggregate state.
    /// </summary>
    /// <param name="mouse">The mouse device to unregister.</param>
    public void Unregister(IMouseInputDevice mouse)
    {
        if (_mice.Remove(mouse.Id))
            _mouseList.Remove(mouse);
    }

    /// <summary>
    /// Gets whether the specified button is down on any connected mouse.
    /// </summary>
    /// <param name="button">The button to check.</param>
    /// <returns><see langword="true"/> if any connected mouse has the button down.</returns>
    public bool IsButtonDown(MouseButtonEnum button) =>
        _mouseList.Any(mouse => mouse.IsConnected && mouse.IsButtonDown(button));
}
