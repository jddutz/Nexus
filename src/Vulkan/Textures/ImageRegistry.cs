namespace Nexus.Graphics.Vulkan.Textures;

public unsafe class ImageRegistry(Context context, ILogger<ImageRegistry> logger) : IImageRegistry
{
    private readonly Context _context = context;
    private readonly ILogger<ImageRegistry> _logger = logger;
    private readonly Dictionary<GraphicsId, ImageEntry> _images = [];
    private readonly Dictionary<VkImage, int> _refs = [];

    private readonly record struct ImageEntry(VkImage Image, DeviceMemory Memory);

    public VkImage Get(GraphicsId id)
    {
        if (!_images.TryGetValue(id, out var entry))
            throw new KeyNotFoundException($"Image '{id}' is not registered.");

        return entry.Image;
    }

    public VkImage Acquire(ITexture texture, ColorFormatEnum format)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var id = new IdentityHashBuilder(nameof(ImageRegistry))
            .Add(texture.Id)
            .Add((ulong)format)
            .Compute();

        if (_images.TryGetValue(id, out var entry))
        {
            var referenceCount = ++_refs[entry.Image];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reusing image. ResourceId={ResourceId}, TextureId={TextureId}, Format={Format}, ImageHandle={ImageHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    texture.Id,
                    format,
                    entry.Image.Handle,
                    referenceCount
                );

            return entry.Image;
        }

        entry = CreateImage(texture, format);

        _images.Add(id, entry);
        _refs.Add(entry.Image, 1);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Created image. ResourceId={ResourceId}, TextureId={TextureId}, Format={Format}, ImageHandle={ImageHandle}",
                id,
                texture.Id,
                format,
                entry.Image.Handle
            );

        return entry.Image;
    }

    public void Release(GraphicsId id)
    {
        if (!_images.TryGetValue(id, out var entry))
            return;

        var referenceCount = --_refs[entry.Image];

        if (referenceCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Released image reference. ResourceId={ResourceId}, ImageHandle={ImageHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    entry.Image.Handle,
                    referenceCount
                );

            return;
        }

        _refs.Remove(entry.Image);
        _images.Remove(id);

        DestroyImage(entry);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Destroyed image. ResourceId={ResourceId}, ImageHandle={ImageHandle}",
                id,
                entry.Image.Handle
            );
    }

    public void Reset()
    {
        foreach (var (id, entry) in _images)
        {
            DestroyImage(entry);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reset image. ResourceId={ResourceId}, ImageHandle={ImageHandle}",
                    id,
                    entry.Image.Handle
                );
        }

        _images.Clear();
        _refs.Clear();
    }

    private ImageEntry CreateImage(ITexture texture, ColorFormatEnum format)
    {
        throw new NotImplementedException();
    }

    private void DestroyImage(ImageEntry entry)
    {
        _context.VulkanApi.DestroyImage(_context.Device, entry.Image, null);

        if (entry.Memory.Handle != 0)
            _context.VulkanApi.FreeMemory(_context.Device, entry.Memory, null);
    }

    public void Dispose()
    {
        Reset();

        GC.SuppressFinalize(this);
    }
}
