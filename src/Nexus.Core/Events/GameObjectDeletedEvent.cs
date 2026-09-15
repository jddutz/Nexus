namespace Nexus.Core.Events;

public class GameObjectDeletedEvent(IGameObject gameObject) : IEvent
{
    public IGameObject GameObject { get; } = gameObject;
}
