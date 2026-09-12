namespace Nexus.Core.Events;

public interface IEventHub
{
    void Register(object handler);
    void Unregister(object handler);
    void Publish(IEvent @event);
}
