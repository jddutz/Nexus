namespace Nexus.GameModel;

public interface IGameSystem
{
    SceneId InitialSceneId { get; }
    void Initialize();
    void Update(double deltaTime);
}
