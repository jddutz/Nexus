namespace Nexus.GameModel;

public class GameSystem : IGameSystem
{
    public SceneId InitialSceneId { get; set; }

    public void Initialize() { }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime) { }
}
