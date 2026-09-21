namespace Nexus.Core.Events;

public class ComponentDeactivatedEvent(IComponent component) : IEvent
{
    public IComponent Component { get; } = component;
}
