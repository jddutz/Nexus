namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    VkImage GetOrCreate(ITexture texture, ColorFormatEnum format);

    VkImage Get(ResourceId id);

    void Release(ResourceId id);
}
