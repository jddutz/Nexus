namespace Nexus.Graphics.Vulkan.Textures;

/// <summary>
/// Manages Vulkan images keyed by texture and color-format identities.
/// </summary>
public unsafe class ImageRegistry : IImageRegistry
{
    private readonly Context _context;
    private readonly ISyncManager _syncManager;
    private readonly PerformanceMetrics? _performanceMetrics;

    private readonly Dictionary<ulong, VkImage> _images = [];
    private readonly Dictionary<VkImage, DeviceMemory> _memory = [];

    private readonly Dictionary<VkBuffer, DeviceMemory> _stagingMemory = [];
    private readonly Queue<VkBuffer>[] _stagedBuffers;

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
        PerformanceMetrics? performanceMetrics = null
    )
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _performanceMetrics = performanceMetrics;
        _released = new Queue<VkImage>[checked((int)syncManager.MaxFramesInFlight)];
        _stagedBuffers = new Queue<VkBuffer>[checked((int)syncManager.MaxFramesInFlight)];

        for (var index = 0; index < _released.Length; index++)
        {
            _released[index] = new Queue<VkImage>();
            _stagedBuffers[index] = new Queue<VkBuffer>();
        }

        _syncManager.FrameCompleted += OnFrameCompleted;
    }

    private VkImage CreateImage(uint width, uint height, ColorFormatEnum format)
    {
        var createInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
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

    private VkImageView CreateImageView(VkImage image, ColorFormatEnum format)
    {
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
                LevelCount = 1,
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
    public IEnumerable<IVulkanCommand> Create(ITexture texture, ColorFormatEnum format)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var id = ComputeImageId(texture.Id, format);

        if (_images.TryGetValue(id, out var image))
        {
            _refs[image]++;
            return [];
        }

        var data = new byte[checked((int)(texture.Count * (ulong)format.GetBytesPerPixel()))];

        texture.WriteTo(0, texture.Count, format, data);

        var stagingBuffer = CreateStagingBuffer(data);
        var stagingFrameIndex = _syncManager.CurrentFrameIndex;

        try
        {
            image = CreateImage(texture.Width, texture.Height, format);
            CreateImageView(image, format);

            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,

                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },

                ImageOffset = new Offset3D(0, 0, 0),

                ImageExtent = new Extent3D
                {
                    Width = texture.Width,
                    Height = texture.Height,
                    Depth = 1,
                },
            };

            var commands = new IVulkanCommand[]
            {
                new UploadImageCommand(stagingBuffer, image, region),
            };

            var imageRegistered = false;
            var referenceRegistered = false;

            try
            {
                _images.Add(id, image);
                imageRegistered = true;

                _refs.Add(image, 1);
                referenceRegistered = true;

                QueueStagedBuffer(stagingBuffer, stagingFrameIndex);
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
    public VkImageView Get(ITexture texture, ColorFormatEnum format)
    {
        ArgumentNullException.ThrowIfNull(texture);

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
                    LevelCount = 1,
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
    public IEnumerable<IVulkanCommand> Update(ITexture texture, ColorFormatEnum format)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var id = ComputeImageId(texture.Id, format);
        if (!_images.TryGetValue(id, out var image))
            return Create(texture, format);

        var data = new byte[checked((int)(texture.Count * (ulong)format.GetBytesPerPixel()))];
        texture.WriteTo(0, texture.Count, format, data);

        var stagingBuffer = CreateStagingBuffer(data);
        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(texture.Width, texture.Height, 1),
        };

        QueueStagedBuffer(stagingBuffer, _syncManager.CurrentFrameIndex);
        return
        [
            new UploadImageCommand(stagingBuffer, image, region, ImageLayout.ShaderReadOnlyOptimal),
        ];
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

    /// <summary>
    /// Associates a staging buffer with the frame slot that submits its upload command.
    /// </summary>
    /// <param name="buffer">The staging buffer used by the upload command.</param>
    /// <param name="frameIndex">The frame slot that owns the upload.</param>
    private void QueueStagedBuffer(VkBuffer buffer, uint frameIndex)
    {
        if (frameIndex >= _syncManager.MaxFramesInFlight)
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                "Invalid frame index."
            );

        _stagedBuffers[checked((int)frameIndex)].Enqueue(buffer);
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
