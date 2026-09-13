namespace Nexus.GameModel;

public interface ISceneRegistry
{
    IScene? Load(SceneId sceneId);
}
