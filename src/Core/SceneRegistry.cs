namespace Nexus.Core;

public class SceneRegistry : ISceneRegistry
{
    public IScene? Load(uint sceneId)
    {
        return new Scene(sceneId);
    }
}
