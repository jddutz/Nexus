namespace Nexus.Core.Events;

public class ComponentActivatedEvent(IComponent component) : IEvent
{
    public IComponent Component { get; } = component;
}
