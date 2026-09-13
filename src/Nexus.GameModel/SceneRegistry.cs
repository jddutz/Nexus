using Nexus.Graphics;

namespace Nexus.GameModel;

public class SceneRegistry : ISceneRegistry
{
    public IScene? Load(SceneId sceneId)
    {
        return new Scene() { Id = sceneId, BackgroundColor = Colors.CornflowerBlue };
    }
}
