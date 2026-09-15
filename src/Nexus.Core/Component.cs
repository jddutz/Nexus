namespace Nexus.Core;

public abstract class Component : IComponent
{
    public ComponentId Id { get; } = ComponentId.New();

    public abstract bool Activate();
    public abstract bool Deactivate();
}
