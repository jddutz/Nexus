namespace Nexus.Graphics.Textures;

/// <summary>
/// Loads and caches textures for the lifetime of the registry.
/// </summary>
/// <param name="manifest">Provides the configured content root and texture file mappings.</param>
/// <param name="logger">Records texture loading diagnostics.</param>
public sealed class TextureRegistry(IContentManifest manifest, ILogger<TextureRegistry> logger)
    : ITextureRegistry
{
    private readonly IContentManifest _manifest =
        manifest ?? throw new ArgumentNullException(nameof(manifest));
    private readonly ILogger<TextureRegistry> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<ContentId, TextureId> _contentTextures = [];
    private readonly Dictionary<TextureId, ITexture> _textures = [];

    /// <inheritdoc/>
    public ITexture GetOrCreate(ContentId contentId)
    {
        if (_contentTextures.TryGetValue(contentId, out var textureId))
            return _textures[textureId];

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
            return (Texture)_textures[textureId];

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
        try
        {
            using var stream = File.OpenRead(Path.GetFullPath(filepath));
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
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

            if (_logger.IsEnabled(LogLevel.Information))
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
