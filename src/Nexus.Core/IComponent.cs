namespace Nexus.Core;

public interface IComponent
{
    ComponentId Id { get; }
    bool IsActivated { get; set; }
}
