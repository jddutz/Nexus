namespace Nexus.Core;

public interface IScene : IGameObject
{
    event Action<IGameObject>? GameObjectAdded;
    event Action<IGameObject>? GameObjectRemoved;
}
