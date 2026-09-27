namespace Nexus.Input;

public interface IInputSystem
{
    /// <summary>
    /// Aggregated keyboard state. Reads the overall state from all currently connected keyboards.
    /// </summary>
    IKeyboardInputState Keyboard { get; }

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
