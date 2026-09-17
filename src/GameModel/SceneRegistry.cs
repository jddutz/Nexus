namespace Nexus.GameModel;

public class SceneRegistry : ISceneRegistry
{
    public IScene? Load(uint sceneId)
    {
        return new Scene(sceneId);
    }
}
