namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    IEnumerable<IVulkanCommand> Create(ITexture texture);
    IEnumerable<IVulkanCommand> Update(ITexture texture);
    IEnumerable<IVulkanCommand> Release(ITexture texture);

    void Reset();
}
