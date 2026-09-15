namespace Nexus.Core;

public interface IComponent
{
    ComponentId Id { get; }
    bool Activate();
    bool Deactivate();
}
