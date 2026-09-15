namespace Nexus.GameModel.GameObjects;

public class SolidColorMeshInstance : IGameObject
{
    public GameObjectId Id { get; }

    public Vector4D<float> Color { get; set; }

    public Matrix4X4<float> Transform { get; set; }

    public void Update(double deltaTime) { }
}
