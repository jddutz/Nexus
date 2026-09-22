namespace Nexus.Game;

/// <summary>
/// Defines the lifecycle and update operations for the game system.
/// </summary>
public interface IGameSystem
{
    /// <summary>
    /// Gets the scene activated when the game starts.
    /// </summary>
    IScene InitialScene { get; }

    /// <summary>
    /// Gets the currently active scene.
    /// </summary>
    IScene? CurrentScene { get; }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the game system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);
}
