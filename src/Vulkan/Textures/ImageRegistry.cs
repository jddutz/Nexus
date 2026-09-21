namespace Nexus.Graphics.Vulkan.Textures;

public unsafe class ImageRegistry(Context context, ILogger<ImageRegistry> logger) : IImageRegistry
{
    private readonly Context _context = context;
    private readonly ILogger<ImageRegistry> _logger = logger;
    private readonly Dictionary<TextureId, VkImage> _images = [];
    private readonly Dictionary<VkImage, DeviceMemory> _memory = [];
    private readonly Dictionary<VkImage, int> _refs = [];

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memoryProperties
        );

        for (uint index = 0; index < memoryProperties.MemoryTypeCount; index++)
        {
            if (
                (typeFilter & (1u << (int)index)) != 0
                && (memoryProperties.MemoryTypes[(int)index].PropertyFlags & properties)
                    == properties
            )
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"Unable to find Vulkan memory type with properties '{properties}'."
        );
    }

    private VkImage CreateImage(uint width, uint height, Format format)
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit,
        };

        var result = _context.VulkanApi.CreateImage(
            _context.Device,
            &imageInfo,
            null,
            out var image
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan image: {result}");

        _context.VulkanApi.GetImageMemoryRequirements(_context.Device, image, out var requirements);

        var allocateInfo = new MemoryAllocateInfo
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
            &allocateInfo,
            null,
            out var memory
        );

        if (result != Result.Success)
        {
            _context.VulkanApi.DestroyImage(_context.Device, image, null);
            throw new InvalidOperationException(
                $"Failed to allocate Vulkan image memory: {result}"
            );
        }

        result = _context.VulkanApi.BindImageMemory(_context.Device, image, memory, 0);

        if (result != Result.Success)
        {
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);
            _context.VulkanApi.DestroyImage(_context.Device, image, null);

            throw new InvalidOperationException($"Failed to bind Vulkan image memory: {result}");
        }

        _memory.Add(image, memory);

        return image;
    }

    public VkImage Acquire(ITexture texture, ColorFormatEnum format)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (_images.TryGetValue(texture.Id, out var image))
        {
            var referenceCount = ++_refs[image];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Acquired existing image. TextureId={TextureId}, ImageHandle={ImageHandle}, ReferenceCount={ReferenceCount}",
                    texture.Id,
                    image.Handle,
                    referenceCount
                );

            return image;
        }

        image = CreateImage(texture.Width, texture.Height, format.ToVulkanFormat());

        // Upload texture.GetPixelData(format) here.

        _images.Add(texture.Id, image);
        _refs.Add(image, 1);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Created image. TextureId={TextureId}, ImageHandle={ImageHandle}, ReferenceCount=1",
                texture.Id,
                image.Handle
            );

        return image;
    }

    public VkImage Get(TextureId id)
    {
        if (!_images.TryGetValue(id, out var image))
            throw new KeyNotFoundException($"Image '{id}' is not registered.");

        return image;
    }

    public void Release(TextureId id)
    {
        if (!_images.Remove(id, out var image))
            return;

        var referenceCount = --_refs[image];

        if (referenceCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Released image reference. TextureId={TextureId}, ImageHandle={ImageHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    image.Handle,
                    referenceCount
                );

            return;
        }

        _refs.Remove(image);

        _context.VulkanApi.DestroyImage(_context.Device, image, null);

        if (_memory.Remove(image, out var memory))
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Destroyed image. TextureId={TextureId}, ImageHandle={ImageHandle}",
                id,
                image.Handle
            );
    }

    public void Reset()
    {
        foreach (var (id, image) in _images)
        {
            _context.VulkanApi.DestroyImage(_context.Device, image, null);

            if (_memory.Remove(image, out var memory))
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reset image. TextureId={TextureId}, ImageHandle={ImageHandle}",
                    id,
                    image.Handle
                );
        }

        _images.Clear();
        _memory.Clear();
        _refs.Clear();
    }

    public void Dispose()
    {
        Reset();

        GC.SuppressFinalize(this);
    }
}
