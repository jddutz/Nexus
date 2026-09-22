namespace Nexus.Graphics.Vulkan.Textures;

public class ImageRegistry : IImageRegistry
{
    private readonly Dictionary<ulong, VkImage> _images = [];
    private readonly Dictionary<VkImage, DeviceMemory> _memory = [];

    private readonly Dictionary<ulong, VkImageView> _views = [];
    private readonly Dictionary<VkImage, List<ulong>> _imageViews = [];

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

        foreach (var pair in _images.Where(x => x.Key.TextureId == texture.Id).ToArray())
        {
            var image = pair.Value;

            if (!_refs.TryGetValue(image, out var refs))
                continue;

            if (refs > 1)
            {
                _refs[image] = refs - 1;
                continue;
            }

            _refs.Remove(image);
            _images.Remove(pair.Key);

            QueueRelease(image);
        }

        return [];
    }

    private void Destroy(VkImage image)
    {
        if (_imageViews.Remove(image, out var viewIds))
        {
            foreach (var viewId in viewIds)
            {
                if (_views.Remove(viewId, out var view))
                    _context.Vk.DestroyImageView(_context.Device, view, null);
            }
        }

        _context.Vk.DestroyImage(_context.Device, image, null);

        if (_memory.Remove(image, out var memory))
            _context.Vk.FreeMemory(_context.Device, memory, null);
    }

    public void Reset()
    {
        foreach (var image in _memory.Keys.ToArray())
            Destroy(image);

        _images.Clear();
        _refs.Clear();
        _views.Clear();
        _imageViews.Clear();

        foreach (var queue in _released)
            queue.Clear();
    }

    public void Dispose()
    {
        _syncManager.FrameCompleted -= OnFrameCompleted;

        Reset();

        GC.SuppressFinalize(this);
    }

    private static ulong ComputeImageId(TextureId id, ColorFormatEnum color) =>
        new IdentityHashBuilder("ImageId").Add(id).Add((uint)color).Compute();

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
