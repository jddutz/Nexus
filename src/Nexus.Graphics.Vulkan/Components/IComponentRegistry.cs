namespace Nexus.Graphics.Vulkan.Components;

public interface IComponentRegistry
{
    bool CanLoad(IGraphicsComponent component);

    RenderItem[] Load(IGraphicsComponent component);

    bool CanUnload(ComponentId componentId);
    void Unload(ComponentId componentId);
}
