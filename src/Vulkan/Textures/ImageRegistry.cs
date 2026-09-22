namespace Nexus.Graphics.Vulkan.Textures;

public class ImageRegistry : IImageRegistry
{
    private readonly Dictionary<(TextureId TextureId, ColorFormatEnum Format), VkImage> _images =
    [];
    private readonly Dictionary<VkImage, DeviceMemory> _memory = [];
    private readonly Dictionary<(VkImage, ViewDefinition), List<VkImageView>> _views = [];
    private readonly Dictionary<VkImage, int> _refs = [];
    private readonly Queue<VkImage>[] _released;

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

    public IEnumerable<IVulkanCommand> Release(ITexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        throw new NotImplementedException();
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private static ulong ComputeImageViewId(in ImageViewCreateInfo info) =>
        new IdentityHashBuilder("ImageViewId")
            .Add(info.Image.Handle)
            .Add((uint)info.ViewType)
            .Add((uint)info.Format)
            .Add((uint)info.Components.R)
            .Add((uint)info.Components.G)
            .Add((uint)info.Components.B)
            .Add((uint)info.Components.A)
            .Add((uint)info.SubresourceRange.AspectMask)
            .Add(info.SubresourceRange.BaseMipLevel)
            .Add(info.SubresourceRange.LevelCount)
            .Add(info.SubresourceRange.BaseArrayLayer)
            .Add(info.SubresourceRange.LayerCount)
            .Add((uint)info.Flags)
            .Compute();
}
