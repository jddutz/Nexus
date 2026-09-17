namespace Nexus.Graphics.Textures;

public class Texture
{
    public ResourceId Id { get; }
    public string Name { get; }
    public uint Width { get; }
    public uint Height { get; }
    public ITextureSource Source { get; }

    public Texture(string name, uint width, uint height, ITextureSource source)
    {
        Name = name;
        Width = width;
        Height = height;
        Source = source;

        Id = new IdentityHashBuilder(nameof(Texture))
            .Add(Name)
            .Add(Width)
            .Add(Height)
            .Add(Source.Id)
            .Compute();
    }
}
