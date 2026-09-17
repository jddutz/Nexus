namespace Nexus.Core.Events;

public class GameObjectCreatedEvent(IGameObject gameObject) : IEvent
{
    public IGameObject GameObject { get; } = gameObject;
}
