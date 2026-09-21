namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    VkImage Acquire(ITexture texture, ColorFormatEnum format);

    VkImage Get(TextureId id);

    void Release(TextureId id);

    void Reset();
}
