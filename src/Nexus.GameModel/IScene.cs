namespace Nexus.GameModel;

public interface IScene : IGameObject
{
    event Action<IComponent>? ComponentAdded;
    event Action<IComponent>? ComponentRemoved;

    event Action<IGameObject>? GameObjectAdded;
    event Action<IGameObject>? GameObjectRemoved;
}
