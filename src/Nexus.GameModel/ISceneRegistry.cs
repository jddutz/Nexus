namespace Nexus.GameModel;

public interface ISceneRegistry
{
    IScene? Load(uint sceneId);
}
