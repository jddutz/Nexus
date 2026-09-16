namespace Nexus.Graphics.Textures;

public class TextureDescription
{
    public ResourceId Id { get; }
    public string Name { get; }
    public uint Width { get; }
    public uint Height { get; }

    public TextureDescription(string name, uint width, uint height)
    {
        Name = name;
        Width = width;
        Height = height;

        Id = new IdentityHashBuilder(nameof(TextureDescription))
            .Add(Name)
            .Add(Width)
            .Add(Height)
            .Compute();
    }
}
