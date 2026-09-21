namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    VkImage Acquire(ITexture texture, ColorFormatEnum format);

    void TransitionToShaderReadOnly(CommandBuffer commandBuffer);

    VkImage Get(TextureId textureId, ColorFormatEnum format);

    void Release(TextureId id);

    void Reset();
}
