namespace Nexus.Graphics.Textures;

public class TextureProvider(IOptions<ContentSettings> options) : IContentProvider<ITextureSource>
{
    private readonly string _path = Path.Combine(options.Value.RootPath, "Textures");

    public ITextureSource Get(ContentId id)
    {
        throw new NotImplementedException();
    }
}
