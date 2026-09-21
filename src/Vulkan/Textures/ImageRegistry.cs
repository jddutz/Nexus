namespace Nexus.Graphics.Vulkan.Textures;

public unsafe class ImageRegistry(Context context, ILogger<ImageRegistry> logger) : IImageRegistry
{
    private readonly Context _context = context;
    private readonly ILogger<ImageRegistry> _logger = logger;
    private readonly Dictionary<RenderableId, VkImage> _images = [];
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

        return image;
    }

    public VkImage Acquire(ITexture texture, ColorFormatEnum format)
    {
        throw new NotImplementedException();
    }

    public VkImage Get(RenderableId id)
    {
        if (!_images.TryGetValue(id, out var image))
            throw new KeyNotFoundException($"Image '{id}' is not registered.");

        return image;
    }

    public void Release(RenderableId id)
    {
        if (!_images.Remove(id, out var image))
            return;

        var referenceCount = --_refs[image];

        if (referenceCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Released image reference. GraphicsId={GraphicsId}, ImageHandle={ImageHandle}, ReferenceCount={ReferenceCount}",
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
                "Destroyed image. GraphicsId={GraphicsId}, ImageHandle={ImageHandle}",
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
                    "Reset image. GraphicsId={GraphicsId}, ImageHandle={ImageHandle}",
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
