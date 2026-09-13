namespace Nexus.Graphics.Resources;

public sealed record ShaderDescription : IGraphicsResource
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ShaderSource Vert { get; }
    public ShaderSource Frag { get; }

    public ShaderDescription(string name, ShaderSource vert, ShaderSource frag)
    {
        Id = new IdentityHashBuilder(nameof(ShaderDescription))
            .Add(name)
            .Add(vert.Id)
            .Add(frag.Id)
            .Compute();

        Name = name;
        Vert = vert;
        Frag = frag;
    }
}
