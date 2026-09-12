namespace Nexus.Core.Scenes;

public interface ISceneGraph
{
    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time since the previous frame.</param>
    void Update(TimeSpan deltaTime);
    SceneId InitialSceneId { get; }
}
