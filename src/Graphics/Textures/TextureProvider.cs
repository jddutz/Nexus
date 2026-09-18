namespace Nexus.Graphics.Textures;

public class TextureProvider(IOptions<ContentSettings> options) : IContentProvider<ITextureSource>
{
    private readonly string _path = Path.Combine(options.Value.RootPath, "Textures");

    public ITextureSource Get(ContentId id)
    {
        var path = Path.Combine(_path, $"{id}.png");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Texture '{id}' was not found.", path);
        }

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

        return new TextureSource(colors);
    }
}
