namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    VkImage GetOrCreate(ITexture texture, ColorFormatEnum format);

    VkImage Get(GraphicsId id);

    void Release(GraphicsId id);
}
