namespace Nexus.Graphics.Vulkan.Textures;

/// <summary>
/// Manages Vulkan images keyed by texture and color-format identities.
/// </summary>
public unsafe class ImageRegistry : IImageRegistry
{
    private readonly Context _context;
    private readonly ISyncManager _syncManager;

    private readonly Dictionary<ulong, VkImage> _images = [];
    private readonly Dictionary<VkImage, DeviceMemory> _memory = [];

    private readonly Dictionary<ulong, VkImageView> _views = [];
    private readonly Dictionary<VkImage, List<ulong>> _imageViews = [];

    private readonly Dictionary<VkImage, int> _refs = [];
    private readonly Queue<VkImage>[] _released;

    /// <summary>
    /// Creates an image registry with frame-slot deferred-release queues.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the images.</param>
    /// <param name="syncManager">The synchronization manager used to defer image destruction.</param>
    public ImageRegistry(Context context, ISyncManager syncManager)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _released = new Queue<VkImage>[checked((int)syncManager.MaxFramesInFlight)];

        for (var index = 0; index < _released.Length; index++)
            _released[index] = new Queue<VkImage>();

        _syncManager.FrameCompleted += OnFrameCompleted;
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Create(ITexture texture, ColorFormatEnum format)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Update(ITexture texture, ColorFormatEnum format)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Release(ITexture texture, ColorFormatEnum format)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var id = ComputeImageId(texture.Id, format);

        if (!_images.TryGetValue(id, out var image))
            return [];

        if (!_refs.TryGetValue(image, out var refs))
            return [];

        if (refs > 1)
        {
            _refs[image] = refs - 1;
            return [];
        }

        _refs.Remove(image);
        _images.Remove(id);

        QueueRelease(image);

        return [];
    }

    private void Destroy(VkImage image)
    {
        if (_imageViews.Remove(image, out var viewIds))
        {
            foreach (var viewId in viewIds)
            {
                if (_views.Remove(viewId, out var view))
                    _context.VulkanApi.DestroyImageView(_context.Device, view, null);
            }
        }

        _context.VulkanApi.DestroyImage(_context.Device, image, null);

        if (_memory.Remove(image, out var memory))
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);
    }

    /// <summary>
    /// Queues an image for destruction when the selected frame slot completes.
    /// </summary>
    /// <param name="image">The image to release.</param>
    private void QueueRelease(VkImage image)
    {
        var releaseFrameIndex = checked(
            (int)(
                (_syncManager.CurrentFrameIndex + _syncManager.MaxFramesInFlight - 1)
                % _syncManager.MaxFramesInFlight
            )
        );

        _released[releaseFrameIndex].Enqueue(image);
    }

    /// <summary>
    /// Destroys images queued for the completed frame slot.
    /// </summary>
    /// <param name="sender">The synchronization manager.</param>
    /// <param name="e">The completed frame event data.</param>
    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
    {
        var releaseFrameIndex = checked((int)e.FrameIndex);
        if (releaseFrameIndex >= _released.Length)
            throw new ArgumentOutOfRangeException(nameof(e), e.FrameIndex, "Invalid frame index.");

        var queue = _released[releaseFrameIndex];
        while (queue.TryDequeue(out var image))
            Destroy(image);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
