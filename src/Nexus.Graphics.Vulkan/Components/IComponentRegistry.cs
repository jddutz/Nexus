namespace Nexus.Graphics.Vulkan.Components;

public interface IComponentRegistry
{
    bool CanLoad(IComponent component);

    RenderItem[] Load(IComponent component);

    bool CanUnload(ComponentId componentId);
    void Unload(ComponentId componentId);
}
