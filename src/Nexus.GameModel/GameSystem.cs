namespace Nexus.GameModel;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem : IGameSystem
{
    /// <summary>
    /// Gets or sets the identifier of the scene activated when the game starts.
    /// </summary>
    public SceneId InitialSceneId { get; set; }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    public void Initialize() { }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime) { }
}
