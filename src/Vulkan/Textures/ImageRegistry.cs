using Nexus.Core.Performance;
namespace Nexus.Graphics.Vulkan.Textures;

/// <summary>
/// Manages Vulkan images keyed by texture and color-format identities.
/// </summary>
public unsafe class ImageRegistry : IImageRegistry
{
    private readonly IPerformanceTelemetry? _telemetry;
    private readonly Context _context;
    private readonly ISyncManager _syncManager;
    private readonly PerformanceMetrics? _performanceMetrics;

    private readonly Dictionary<ulong, VkImage> _images = [];
    private readonly Dictionary<VkImage, DeviceMemory> _memory = [];

    private readonly Dictionary<VkBuffer, DeviceMemory> _stagingMemory = [];
    private readonly Queue<VkBuffer>[] _stagedBuffers;
    private readonly Queue<VkBuffer> _pendingStagedBuffers = [];

    private readonly Dictionary<ulong, VkImageView> _views = [];
    private readonly Dictionary<VkImage, List<ulong>> _imageViews = [];

    private readonly Dictionary<VkImage, int> _refs = [];
    private readonly Queue<VkImage>[] _released;

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memoryProperties
        );

        for (uint index = 0; index < memoryProperties.MemoryTypeCount; index++)
        {
            var supported = (typeFilter & (1u << checked((int)index))) != 0;

            if (!supported)
                continue;

            if ((memoryProperties.MemoryTypes[(int)index].PropertyFlags & properties) == properties)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"No Vulkan memory type supports {properties}.");
    }

    /// <summary>
    /// Creates an image registry with frame-slot deferred-release queues.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the images.</param>
    /// <param name="syncManager">The synchronization manager used to defer image destruction.</param>
    /// <param name="performanceMetrics">Optional metrics tracker for live Vulkan buffers.</param>
    public ImageRegistry(
        Context context,
        ISyncManager syncManager,
        PerformanceMetrics? performanceMetrics = null,
        IPerformanceTelemetry? telemetry = null
    )
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _performanceMetrics = performanceMetrics;
        _telemetry = telemetry;
        using var timing = new LoadPerformanceScope(telemetry, "Textures.ImageRegistry.initialize");
        _released = new Queue<VkImage>[checked((int)syncManager.MaxFramesInFlight)];
        _stagedBuffers = new Queue<VkBuffer>[checked((int)syncManager.MaxFramesInFlight)];

        for (var index = 0; index < _released.Length; index++)
        {
            _released[index] = new Queue<VkImage>();
            _stagedBuffers[index] = new Queue<VkBuffer>();
        }

        _syncManager.FrameCompleted += OnFrameCompleted;
        _syncManager.FrameSubmitted += OnFrameSubmitted;
    }

    private VkImage CreateImage(uint width, uint height, ColorFormatEnum format, uint mipLevels)
    {
        using var timing = new LoadPerformanceScope(_telemetry, "texture.image.allocate", units: (long)width * height);
        var createInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = mipLevels,
            ArrayLayers = 1,
            Format = format.ToVulkanFormat(),
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit,
        };

        var result = _context.VulkanApi.CreateImage(
            _context.Device,
            in createInfo,
            null,
            out var image
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan image: {result}.");

        try
        {
            _context.VulkanApi.GetImageMemoryRequirements(
                _context.Device,
                image,
                out var requirements
            );

            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(
                    requirements.MemoryTypeBits,
                    MemoryPropertyFlags.DeviceLocalBit
                ),
            };

            result = _context.VulkanApi.AllocateMemory(
                _context.Device,
                in allocationInfo,
                null,
                out var memory
            );

            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to allocate Vulkan image memory: {result}."
                );

            try
            {
                result = _context.VulkanApi.BindImageMemory(_context.Device, image, memory, 0);

                if (result != Result.Success)
                    throw new InvalidOperationException(
                        $"Failed to bind Vulkan image memory: {result}."
                    );

                _memory.Add(image, memory);

                return image;
            }
            catch
            {
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);
                throw;
            }
        }
        catch
        {
            _context.VulkanApi.DestroyImage(_context.Device, image, null);
            throw;
        }
    }

    private VkImageView CreateImageView(VkImage image, ColorFormatEnum format, uint mipLevels)
    {
        using var timing = new LoadPerformanceScope(_telemetry, "texture.image.view.create");
        var createInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format.ToVulkanFormat(),

            Components = new ComponentMapping
            {
                R = ComponentSwizzle.Identity,
                G = ComponentSwizzle.Identity,
                B = ComponentSwizzle.Identity,
                A = ComponentSwizzle.Identity,
            },

            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = mipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        var id = ComputeImageViewId(in createInfo);

        if (_views.TryGetValue(id, out var existing))
            return existing;

        var result = _context.VulkanApi.CreateImageView(
            _context.Device,
            in createInfo,
            null,
            out var view
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan image view: {result}.");

        _views.Add(id, view);

        if (!_imageViews.TryGetValue(image, out var viewIds))
        {
            viewIds = [];
            _imageViews.Add(image, viewIds);
        }

        viewIds.Add(id);

        return view;
    }

    private VkBuffer CreateStagingBuffer(ReadOnlySpan<byte> data)
    {
        using var timing = new LoadPerformanceScope(_telemetry, "texture.staging.allocate.upload", units: data.Length);
        var createInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = checked((ulong)data.Length),
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive,
        };

        var result = _context.VulkanApi.CreateBuffer(
            _context.Device,
            in createInfo,
            null,
            out var buffer
        );

        if (result != Result.Success)
            throw new InvalidOperationException(
                $"Failed to create Vulkan staging buffer: {result}."
            );

        try
        {
            _context.VulkanApi.GetBufferMemoryRequirements(
                _context.Device,
                buffer,
                out var requirements
            );

            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(
                    requirements.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
                ),
            };

            result = _context.VulkanApi.AllocateMemory(
                _context.Device,
                in allocationInfo,
                null,
                out var memory
            );

            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to allocate Vulkan staging memory: {result}."
                );

            try
            {
                result = _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);

                if (result != Result.Success)
                    throw new InvalidOperationException(
                        $"Failed to bind Vulkan staging memory: {result}."
                    );

                void* mapped = null;

                result = _context.VulkanApi.MapMemory(
                    _context.Device,
                    memory,
                    0,
                    checked((ulong)data.Length),
                    0,
                    &mapped
                );

                if (result != Result.Success)
                    throw new InvalidOperationException(
                        $"Failed to map Vulkan staging memory: {result}."
                    );

                try
                {
                    data.CopyTo(new Span<byte>(mapped, data.Length));
                }
                finally
                {
                    _context.VulkanApi.UnmapMemory(_context.Device, memory);
                }

                _stagingMemory.Add(buffer, memory);
                _performanceMetrics?.RecordBufferCreated();

                return buffer;
            }
            catch
            {
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);

                throw;
            }
        }
        catch
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);

            throw;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Create(ITexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        using var timing = new LoadPerformanceScope(_telemetry, "texture.image.realize", units: checked((long)texture.Count));
        var format = texture.TextureFormat;
        var id = ComputeImageId(texture.Id, format);

        if (_images.TryGetValue(id, out var image))
        {
            _telemetry?.RecordCache("texture.image", null, true);
            _refs[image]++;
            return [];
        }

        _telemetry?.RecordCache("texture.image", null, false);
        byte[] data;
        BufferImageCopy[] regions;
        using (var serialization = new LoadPerformanceScope(_telemetry, "texture.serialize", units: checked((long)texture.Count)))
            (data, regions) = SerializeMipLevels(texture);

        var stagingBuffer = CreateStagingBuffer(data);
        try
        {
            image = CreateImage(texture.Width, texture.Height, format, texture.MipLevelCount);
            CreateImageView(image, format, texture.MipLevelCount);

            var commands = regions.Select(region => (IVulkanCommand)new UploadImageCommand(stagingBuffer, image, region)).ToArray();

            var imageRegistered = false;
            var referenceRegistered = false;

            try
            {
                _images.Add(id, image);
                imageRegistered = true;

                _refs.Add(image, 1);
                referenceRegistered = true;

                QueueStagedBuffer(stagingBuffer);
            }
            catch
            {
                if (referenceRegistered)
                    _refs.Remove(image);

                if (imageRegistered)
                    _images.Remove(id);

                throw;
            }

            return commands;
        }
        catch
        {
            DestroyStagingBuffer(stagingBuffer);

            if (image.Handle != 0)
                Destroy(image);

            throw;
        }
    }

    /// <inheritdoc/>
    public VkImageView Get(ITexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var format = texture.TextureFormat;
        var imageId = ComputeImageId(texture.Id, format);

        if (!_images.TryGetValue(imageId, out var image))
            throw new KeyNotFoundException($"Image for texture '{texture.Id}' is not registered.");

        var viewId = ComputeImageViewId(
            new ImageViewCreateInfo
            {
                Image = image,
                Format = format.ToVulkanFormat(),
                ViewType = ImageViewType.Type2D,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity,
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    LevelCount = texture.MipLevelCount,
                    LayerCount = 1,
                },
            }
        );

        if (!_views.TryGetValue(viewId, out var view))
            throw new KeyNotFoundException(
                $"Image view for texture '{texture.Id}' is not registered."
            );

        return view;
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Update(ITexture texture)
    {
        using var timing = new LoadPerformanceScope(_telemetry, "texture.image.update");
        ArgumentNullException.ThrowIfNull(texture);

        var format = texture.TextureFormat;
        var id = ComputeImageId(texture.Id, format);
        if (!_images.TryGetValue(id, out var image))
            return Create(texture);

        byte[] data;
        BufferImageCopy[] regions;
        using (var serialization = new LoadPerformanceScope(_telemetry, "texture.serialize", units: checked((long)texture.Count)))
            (data, regions) = SerializeMipLevels(texture);
        var stagingBuffer = CreateStagingBuffer(data);
        QueueStagedBuffer(stagingBuffer);
        return regions.Select(region => (IVulkanCommand)new UploadImageCommand(stagingBuffer, image, region, ImageLayout.ShaderReadOnlyOptimal)).ToArray();
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Release(ITexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var format = texture.TextureFormat;
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

    private void DestroyStagingBuffer(VkBuffer buffer)
    {
        _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);

        if (_stagingMemory.Remove(buffer, out var memory))
        {
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);
            _performanceMetrics?.RecordBufferDestroyed();
        }
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

    /// <summary>Defers a staging buffer until its upload command is submitted.</summary>
    /// <param name="buffer">The staging buffer used by the upload command.</param>
    private void QueueStagedBuffer(VkBuffer buffer) => _pendingStagedBuffers.Enqueue(buffer);

    /// <summary>Associates pending staging buffers with the frame that submitted their uploads.</summary>
    /// <param name="sender">The synchronization manager.</param>
    /// <param name="e">The submitted frame event data.</param>
    private void OnFrameSubmitted(object? sender, FrameSubmittedEventArgs e)
    {
        var frameIndex = checked((int)e.FrameIndex);
        if (frameIndex >= _stagedBuffers.Length)
            throw new ArgumentOutOfRangeException(nameof(e), e.FrameIndex, "Invalid frame index.");

        var stagingBuffers = _stagedBuffers[frameIndex];
        while (_pendingStagedBuffers.TryDequeue(out var buffer))
            stagingBuffers.Enqueue(buffer);
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

        var images = _released[releaseFrameIndex];

        while (images.TryDequeue(out var image))
            Destroy(image);

        var stagingBuffers = _stagedBuffers[releaseFrameIndex];

        while (stagingBuffers.TryDequeue(out var buffer))
            DestroyStagingBuffer(buffer);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        foreach (var image in _memory.Keys.ToArray())
            Destroy(image);

        foreach (var buffer in _stagingMemory.Keys.ToArray())
            DestroyStagingBuffer(buffer);

        _images.Clear();
        _refs.Clear();
        _views.Clear();
        _imageViews.Clear();
        _stagingMemory.Clear();

        foreach (var queue in _released)
            queue.Clear();

        foreach (var queue in _stagedBuffers)
            queue.Clear();
        _pendingStagedBuffers.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _syncManager.FrameCompleted -= OnFrameCompleted;
        _syncManager.FrameSubmitted -= OnFrameSubmitted;

        Reset();

        GC.SuppressFinalize(this);
    }

    internal static (byte[] Data, BufferImageCopy[] Regions) SerializeMipLevels(ITexture texture)
    {
        var regions = new BufferImageCopy[texture.MipLevelCount];
        uint width = texture.Width, height = texture.Height;
        ulong offset = 0;
        for (uint level = 0; level < texture.MipLevelCount; level++)
        {
            regions[level] = new BufferImageCopy
            {
                BufferOffset = offset,
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, level, 0, 1),
                ImageExtent = new Extent3D(width, height, 1),
            };
            offset = checked(offset + (ulong)width * height * (ulong)texture.TextureFormat.GetBytesPerPixel());
            // Vulkan buffer offsets must be multiples of four, even for RGB formats.
            offset = checked((offset + 3) & ~3ul);
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        var data = new byte[checked((int)offset)];
        for (uint level = 0; level < texture.MipLevelCount; level++)
        {
            var region = regions[level];
            var length = checked((int)((ulong)region.ImageExtent.Width * region.ImageExtent.Height * (ulong)texture.TextureFormat.GetBytesPerPixel()));
            texture.WriteMipLevel(level, texture.TextureFormat, data.AsSpan(checked((int)region.BufferOffset), length));
        }
        return (data, regions);
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
