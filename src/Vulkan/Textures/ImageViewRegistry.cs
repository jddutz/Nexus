namespace Nexus.Graphics.Vulkan.Textures;

public unsafe class ImageViewRegistry(Context context, ILogger<ImageViewRegistry> logger)
    : IImageViewRegistry
{
    private readonly Context _context = context;
    private readonly ILogger<ImageViewRegistry> _logger = logger;
    private readonly Dictionary<DrawableId, ImageView> _imageViews = [];
    private readonly Dictionary<ImageView, int> _refs = [];

    public ImageView Get(DrawableId id)
    {
        if (!_imageViews.TryGetValue(id, out var imageView))
            throw new KeyNotFoundException($"Image view '{id}' is not registered.");

        return imageView;
    }

    public ImageView Acquire(DrawableId imageId, Image image, Format format)
    {
        var id = new IdentityHashBuilder(nameof(ImageViewRegistry))
            .Add(imageId)
            .Add((ulong)format)
            .Compute();

        if (_imageViews.TryGetValue(id, out var imageView))
        {
            var referenceCount = ++_refs[imageView];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reusing image view. ResourceId={ResourceId}, ImageId={ImageId}, Format={Format}, ImageViewHandle={ImageViewHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    imageId,
                    format,
                    imageView.Handle,
                    referenceCount
                );

            return imageView;
        }

        imageView = CreateImageView(image, format);

        _imageViews.Add(id, imageView);
        _refs.Add(imageView, 1);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Created image view. ResourceId={ResourceId}, ImageId={ImageId}, Format={Format}, ImageViewHandle={ImageViewHandle}",
                id,
                imageId,
                format,
                imageView.Handle
            );

        return imageView;
    }

    public void Release(DrawableId id)
    {
        if (!_imageViews.TryGetValue(id, out var imageView))
            return;

        var referenceCount = --_refs[imageView];

        if (referenceCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Released image view reference. ResourceId={ResourceId}, ImageViewHandle={ImageViewHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    imageView.Handle,
                    referenceCount
                );

            return;
        }

        _refs.Remove(imageView);
        _imageViews.Remove(id);

        _context.VulkanApi.DestroyImageView(_context.Device, imageView, null);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Destroyed image view. ResourceId={ResourceId}, ImageViewHandle={ImageViewHandle}",
                id,
                imageView.Handle
            );
    }

    public void Reset()
    {
        foreach (var (id, imageView) in _imageViews)
        {
            _context.VulkanApi.DestroyImageView(_context.Device, imageView, null);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reset image view. ResourceId={ResourceId}, ImageViewHandle={ImageViewHandle}",
                    id,
                    imageView.Handle
                );
        }

        _imageViews.Clear();
        _refs.Clear();
    }

    private ImageView CreateImageView(Image image, Format format)
    {
        var createInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
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

        var result = _context.VulkanApi.CreateImageView(
            _context.Device,
            in createInfo,
            null,
            out var imageView
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Unable to create image view: {result}");

        return imageView;
    }

    public void Dispose()
    {
        Reset();

        GC.SuppressFinalize(this);
    }
}
