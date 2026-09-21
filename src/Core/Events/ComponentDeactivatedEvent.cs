namespace Nexus.Core.Events;

public class ComponentDeactivatedEvent(Component component) : IEvent
{
    public Component Component { get; } = component;
}
