namespace Nexus.Input;

public interface IInputSystem
{
    /// <summary>
    /// Initializes the input system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the input system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    bool Activate<TComponent>(TComponent component)
        where TComponent : class, IInputComponent;

    bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IInputComponent;
}
