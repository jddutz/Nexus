namespace Nexus.Core.Scenes;

public interface ISceneGraph
{
    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);
    SceneId InitialSceneId { get; }
}
