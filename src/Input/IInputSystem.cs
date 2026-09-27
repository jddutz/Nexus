namespace Nexus.Input;

public interface IInputSystem
{
    /// <summary>
    /// Gets or sets the scene input map that receives dispatched keyboard events.
    /// </summary>
    SceneInputMap? CurrentMap { get; set; }

    /// <summary>
    /// Aggregated keyboard state. Reads the overall state from all currently connected keyboards.
    /// </summary>
    IKeyboardInputState Keyboard { get; }

    /// <summary>
    /// Aggregated mouse state with device-specific access by identifier.
    /// </summary>
    IMouseInputState Mouse { get; }

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
