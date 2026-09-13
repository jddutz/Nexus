using Nexus.Graphics;
using Silk.NET.Maths;

namespace Nexus.GameModel;

public class Scene : IScene
{
    public SceneId SceneId { get; init; }

    public Vector4D<float> BackgroundColor { get; set; }

    public void Update(double deltaTime) { }
}
