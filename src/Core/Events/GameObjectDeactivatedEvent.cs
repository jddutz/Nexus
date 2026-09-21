namespace Nexus.Core.Events;

public class GameObjectDeactivatedEvent(IGameObject gameObject) : IEvent
{
    public IGameObject GameObject { get; } = gameObject;
}
