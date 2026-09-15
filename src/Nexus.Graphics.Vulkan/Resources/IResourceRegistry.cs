namespace Nexus.Graphics.Vulkan.Resources;

public interface IResourceRegistry
{
    bool CanLoad(IResourceDescription resource);
    ResourceId Load(IResourceDescription resource);
}
