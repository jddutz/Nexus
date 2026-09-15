namespace Nexus.Core;

public abstract class Component : IComponent
{
    public ComponentId Id { get; } = ComponentId.New();
    public bool IsActivated { get; set; } = false;
}
