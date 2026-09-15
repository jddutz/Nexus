namespace Nexus.GameModel;

public class Scene : IScene
{
    public GameObjectId Id { get; init; }

    public Vector4D<float> BackgroundColor { get; set; }

    public void Update(double deltaTime) { }
}
