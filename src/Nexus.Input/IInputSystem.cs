namespace Nexus.Input;

public interface IInputSystem
{
    /// <summary>
    /// Updates the input system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);
}
