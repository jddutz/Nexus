namespace Nexus.Input;

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

    /// <summary>Looks up a currently registered controller by its connection-specific identifier.</summary>
    /// <param name="id">The controller identifier.</param>
    /// <param name="controller">The matching controller, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the controller is registered.</returns>
    bool TryGetController(InputDeviceId id, out IController? controller);

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
