namespace Nexus.Graphics.Vulkan.Resources;

public class VulkanResourceManager(IEnumerable<IResourceRegistry> registries)
    : IGraphicsResourceManager
{
    private IEnumerable<IResourceRegistry> _registries = registries;

    public ResourceId Load(IResourceDescription resource)
    {
        foreach (var registry in _registries)
        {
            if (registry.CanLoad(resource))
                return registry.Load(resource);
        }

        throw new NotSupportedException(
            $"No registry supports resource description: {resource.GetType().Name}"
        );
    }
}
