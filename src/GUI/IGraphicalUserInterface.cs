namespace Nexus.GUI;

/// <summary>
/// Defines lifecycle operations for a graphical user interface.
/// </summary>
public interface IGraphicalUserInterface
{
    /// <summary>
    /// Initializes the graphical user interface.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the graphical user interface for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time, in seconds, since the previous frame.</param>
    void Update(double deltaTime);
}
