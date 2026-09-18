namespace Nexus.Graphics.Textures;

/// <summary>
/// Loads texture sources from PNG files under the configured content root, falling back to a
/// single magenta pixel when the requested file does not exist.
/// </summary>
/// <param name="options">Provides the configured content root path.</param>
public sealed class TextureProvider(
    IOptions<ContentSettings> options,
    ILogger<TextureProvider> logger
) : IContentProvider<ITextureSource>
{
    private readonly string _path = Path.Combine(options.Value.RootPath, "Textures");

    private readonly Dictionary<ContentId, ITextureSource> _cache = [];

    public ITextureSource Get(ContentId id)
    {
        if (_cache.TryGetValue(id, out var cached))
            return cached;

        var source = Load(id);

        _cache[id] = source;

        return source;
    }

    private ITextureSource Load(ContentId id)
    {
        var path = Path.Combine(_path, $"{id}.png");

        logger.LogDebug("Loading texture. Id={Id}, Path={Path}", id, path);

        if (!File.Exists(path))
            return new TextureSource([new Color(255, 0, 255)]);

        using var stream = File.OpenRead(path);

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

        logger.LogInformation(
            "Loaded texture. Id={Id}, Width={Width}, Height={Height}",
            id,
            image.Width,
            image.Height
        );

        return new TextureSource(colors);
    }
}
