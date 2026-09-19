namespace Nexus.Graphics.Textures;

/// <summary>
/// Loads texture sources from image files under the configured content root and caches them for
/// reuse.
/// </summary>
/// <param name="manifest">Provides the configured content root and texture file mappings.</param>
/// <param name="logger">Records texture loading diagnostics.</param>
public sealed class TextureProvider(IContentManifest manifest, ILogger<TextureProvider> logger)
    : IContentProvider<Texture>
{
    private readonly IContentManifest _manifest = manifest;
    private readonly Dictionary<ContentId, Texture> _textures = [];

    /// <summary>
    /// Loads a texture from a file and caches the result by content identifier.
    /// </summary>
    /// <param name="contentId">The identifier to assign to the loaded texture.</param>
    /// <param name="filepath">The path of the image file to load.</param>
    /// <returns>The loaded texture, or an invalid texture when loading fails.</returns>
    private Texture LoadTextureFile(ContentId contentId, string filepath)
    {
        try
        {
            using var stream = File.OpenRead(Path.GetFullPath(filepath));

            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            var colors = new Color[image.Width * image.Height];

            for (var i = 0; i < colors.Length; i++)
            {
                var offset = i * 4;

                colors[i] = new Color(
                    image.Data[offset],
                    image.Data[offset + 1],
                    image.Data[offset + 2],
                    image.Data[offset + 3]
                );
            }

            var texture = new Texture(
                contentId: contentId,
                width: (uint)image.Width,
                height: (uint)image.Height,
                source: new TextureSource(colors)
            );

            _textures[contentId] = texture;

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation(
                    "Texture loaded: Id={Id}, Width={Width}, Height={Height}",
                    Path.GetFileNameWithoutExtension(filepath),
                    image.Width,
                    image.Height
                );

            return texture;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load texture from file {Path}", filepath);
        }

        // If we get here then the texture source is invalid
        var invalidTexture = new Texture(contentId, 1, 1, TextureSource.Invalid);

        _textures[contentId] = invalidTexture;

        return invalidTexture;
    }

    /// <summary>
    /// Gets a texture by content identifier, loading it from the content library when necessary.
    /// </summary>
    /// <param name="id">The identifier of the texture to retrieve.</param>
    /// <returns>The cached, built-in, or loaded texture associated with <paramref name="id"/>.</returns>
    public Texture Get(ContentId id)
    {
        if (_textures.TryGetValue(id, out var cached))
            return cached;

        if (id == BuiltInTextures.Invalid)
        {
            var result = new Texture(
                contentId: id,
                width: 1,
                height: 1,
                source: new TextureSource([Colors.Magenta])
            );

            _textures[id] = result;

            return result;
        }

        if (id == BuiltInTextures.Uniform)
        {
            var result = new Texture(
                contentId: id,
                width: 1,
                height: 1,
                source: new TextureSource([Colors.White])
            );

            _textures[id] = result;

            return result;
        }

        var filepath = Path.Combine(
            _manifest.ContentLibraryPath,
            _manifest.Textures.GetContentFilePath(id)
        );

        return LoadTextureFile(id, filepath);
    }

    /// <summary>
    /// Loads a texture directly from a file path, caching it by its derived content identifier.
    /// </summary>
    /// <param name="filepath">The path of the image file to load.</param>
    /// <returns>The loaded texture, or an invalid texture when loading fails.</returns>
    public Texture Load(string filepath)
    {
        var contentId = ContentId.FromFilePath(filepath);

        if (_textures.TryGetValue(contentId, out var cached))
            return cached;

        return LoadTextureFile(contentId, filepath);
    }
}
