namespace Nexus.Graphics.Textures;

using Nexus.Core.Performance;

/// <summary>
/// Loads and caches textures for the lifetime of the registry.
/// </summary>
/// <param name="manifest">Provides the configured content root and texture file mappings.</param>
/// <param name="logger">Records texture loading diagnostics.</param>
public sealed class TextureRegistry(IContentManifest manifest, ILogger<TextureRegistry> logger, IGraphicsProfiler? profiler = null)
    : ITextureRegistry
{
    private readonly IContentManifest _manifest =
        manifest ?? throw new ArgumentNullException(nameof(manifest));
    private readonly ILogger<TextureRegistry> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<ContentId, TextureId> _contentTextures = [];
    private readonly IGraphicsProfiler? _profiler = profiler;
    private readonly Dictionary<TextureId, ITexture> _textures = [];

    /// <inheritdoc/>
    public ITexture GetOrCreate(ContentId contentId)
    {
        if (_contentTextures.TryGetValue(contentId, out var textureId))
        {
            _profiler?.RecordCache("texture.registry", contentId.Value, true);
            return _textures[textureId];
        }

        _profiler?.RecordCache("texture.registry", contentId.Value, false);
        using var requestTiming = new LoadPerformanceScope(_profiler, "texture.registry.create", contentId.Value);
        if (contentId == ContentId.Invalid)
        {
            var result = new Texture(contentId, 1, 1, [Colors.Magenta]);
            Register(contentId, result);
            return result;
        }

        if (contentId == (ContentId)"uniform")
        {
            var result = new Texture(contentId, 1, 1, [Colors.White]);
            Register(contentId, result);
            return result;
        }

        var filepath = Path.Combine(
            _manifest.ContentLibraryPath,
            _manifest.Textures.GetContentFilePath(contentId)
        );

        var texture = LoadTextureFile(contentId, filepath);
        Register(contentId, texture);
        return texture;
    }

    /// <summary>
    /// Loads a texture directly from a file path and caches it.
    /// </summary>
    /// <param name="filepath">The path of the image file to load.</param>
    /// <returns>The loaded texture.</returns>
    public Texture Load(string filepath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filepath);

        var contentId = ContentId.FromFilePath(filepath);
        if (_contentTextures.TryGetValue(contentId, out var textureId))
        {
            _profiler?.RecordCache("texture.registry", contentId.Value, true);
            return (Texture)_textures[textureId];
        }

        _profiler?.RecordCache("texture.registry", contentId.Value, false);
        using var requestTiming = new LoadPerformanceScope(_profiler, "texture.registry.create", contentId.Value);
        var texture = LoadTextureFile(contentId, filepath);
        Register(contentId, texture);
        return texture;
    }

    /// <summary>
    /// Loads an image file into a texture, using an invalid texture when loading fails.
    /// </summary>
    /// <param name="contentId">The content identifier assigned to the texture.</param>
    /// <param name="filepath">The path of the image file to load.</param>
    /// <returns>The loaded texture or an invalid fallback texture.</returns>
    private Texture LoadTextureFile(ContentId contentId, string filepath)
    {
        using var loadTiming = new LoadPerformanceScope(_profiler, "texture.file.load", contentId.Value);
        try
        {
            using var stream = File.OpenRead(Path.GetFullPath(filepath));
            ImageResult image;
            using (var decodeTiming = new LoadPerformanceScope(_profiler, "texture.image.decode", contentId.Value))
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            using var conversion = new LoadPerformanceScope(_profiler, "texture.rgba.realize", contentId.Value, units: (long)image.Width * image.Height);
            var colors = new Color[image.Width * image.Height];

            for (var index = 0; index < colors.Length; index++)
            {
                var offset = index * 4;
                colors[index] = new Color(
                    image.Data[offset],
                    image.Data[offset + 1],
                    image.Data[offset + 2],
                    image.Data[offset + 3]
                );
            }

            var texture = new Texture(
                contentId,
                (uint)image.Width,
                (uint)image.Height,
                colors,
                ColorFormatEnum.RGBA8Srgb
            );

            if (_profiler?.IsEnabled == true && _logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "Texture loaded: Id={Id}, Width={Width}, Height={Height}",
                    Path.GetFileNameWithoutExtension(filepath),
                    image.Width,
                    image.Height
                );

            return texture;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load texture from file {Path}", filepath);
            return new Texture(contentId, 1, 1, [Colors.Magenta]);
        }
    }

    /// <summary>
    /// Adds a texture to the cache using its content and runtime identifiers.
    /// </summary>
    /// <param name="contentId">The content identifier associated with the texture.</param>
    /// <param name="texture">The texture to register.</param>
    private void Register(ContentId contentId, Texture texture)
    {
        if (_textures.ContainsKey(texture.Id))
        {
            _contentTextures.Add(contentId, texture.Id);
            return;
        }

        _contentTextures.Add(contentId, texture.Id);
        _textures.Add(texture.Id, texture);
    }

    /// <inheritdoc/>
    public ITexture Get(TextureId texture)
    {
        if (!_textures.TryGetValue(texture, out var value))
            throw new KeyNotFoundException($"Texture '{texture}' is not registered.");

        return value;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        using var resetTiming = new LoadPerformanceScope(_profiler, "texture.registry.reset", units: _textures.Count);
        _contentTextures.Clear();
        _textures.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Reset();
        GC.SuppressFinalize(this);
    }
}
