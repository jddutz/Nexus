namespace Nexus.Input.Devices;

/// <summary>
/// Represents one connected controller and its local button and analog controls.
/// </summary>
public interface IController : IInputDevice
{
    /// <summary>Gets the controller's buttons in logical index order.</summary>
    IReadOnlyList<IButtonInput> Buttons { get; }

    /// <summary>Gets the controller's analog inputs in logical index order.</summary>
    IReadOnlyList<IAnalogInput> AnalogInputs { get; }

    /// <summary>Gets the button at the specified logical index.</summary>
    /// <param name="index">The button's controller-local index.</param>
    /// <returns>The requested button.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the button collection.</exception>
    IButtonInput Button(int index);

    /// <summary>Gets the analog input at the specified logical index.</summary>
    /// <param name="index">The analog input's controller-local index.</param>
    /// <returns>The requested analog input.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the analog collection.</exception>
    IAnalogInput AnalogInput(int index);

    /// <summary>Occurs when a mapped button transitions to pressed.</summary>
    event Action<IController, IButtonInput>? ButtonPressed;

    /// <summary>Occurs when a mapped button transitions to released.</summary>
    event Action<IController, IButtonInput>? ButtonReleased;

    /// <summary>Occurs when a mapped analog position changes.</summary>
    event Action<IController, IAnalogInput, Vector2D<float>>? AnalogChanged;
}

/// <summary>Maintains the earlier controller interface name for source compatibility.</summary>
public interface IControllerInputDevice : IController { }
