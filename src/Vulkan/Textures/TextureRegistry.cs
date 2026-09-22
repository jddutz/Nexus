namespace Nexus.Graphics.Vulkan.Textures;

public class TextureRegistry : ITextureRegistry
{
    private sealed record TextureResourceKey(TextureId TextureId, ColorFormatEnum Format);

    private sealed record TextureResources(VkImage Image, VkImageView ImageView, VkSampler Sampler);

    private readonly Dictionary<TextureResourceKey, TextureResources> _resources = [];

    public IEnumerable<IVulkanCommand> Create(ITexture texture)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Update(ITexture texture)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Delete(ITexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Reset()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
