namespace Nexus.GameModel;

public interface ISceneRegistry
{
    IScene? Load(GameObjectId sceneId);
}
