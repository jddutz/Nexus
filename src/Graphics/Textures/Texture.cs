namespace Nexus.Graphics.Textures;

public class Texture
{
    public ContentId Id { get; }
    public uint Width { get; }
    public uint Height { get; }
    public ITextureDataSource Source { get; }

    public Texture(ContentId contentId, uint width, uint height, ITextureDataSource source)
    {
        Id = contentId;
        Width = width;
        Height = height;
        Source = source;
    }
}
