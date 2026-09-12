namespace Nexus.Core.Scenes;

public interface ISceneGraph
{
    void Update();
    SceneId InitialSceneId { get; }
}
