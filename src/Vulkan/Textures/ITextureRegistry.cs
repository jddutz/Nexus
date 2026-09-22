namespace Nexus.Graphics.Vulkan.Textures;

public interface ITextureRegistry : IDisposable
{
    IEnumerable<IVulkanCommand> Create(ITexture texture);
    IEnumerable<IVulkanCommand> Update(ITexture texture);
    IEnumerable<IVulkanCommand> Release(ITexture texture);
    IEnumerable<IVulkanCommand> Reset();
}
