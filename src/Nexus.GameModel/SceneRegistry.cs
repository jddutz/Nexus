using Nexus.Graphics;

namespace Nexus.GameModel;

public class SceneRegistry : ISceneRegistry
{
    public IScene? Load(GameObjectId sceneId)
    {
        return new Scene() { Id = sceneId, BackgroundColor = Colors.CornflowerBlue };
    }
}
