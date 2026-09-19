namespace Nexus.Graphics.Textures;

public class Texture
{
    public ContentId Id { get; }
    public uint Width { get; }
    public uint Height { get; }
    public ITextureSource Source { get; }

    public Texture(ContentId contentId, uint width, uint height, ITextureSource source)
    {
        Id = contentId;
        Width = width;
        Height = height;
        Source = source;
    }
}
