namespace Nexus.GameModel;

public interface IGameObject
{
    public GameObjectId Id { get; }

    public void Update(double deltaTime) { }
}
