namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    IEnumerable<IVulkanCommand> Create(ITexture texture, ColorFormatEnum format);
    IEnumerable<IVulkanCommand> Update(ITexture texture, ColorFormatEnum format);
    IEnumerable<IVulkanCommand> Release(ITexture texture, ColorFormatEnum format);

    void Reset();
}
