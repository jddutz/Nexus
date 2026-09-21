namespace Nexus.Core.Events;

public class GameObjectActivatedEvent(IGameObject gameObject) : IEvent
{
    public IGameObject GameObject { get; } = gameObject;
}
