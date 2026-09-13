namespace Nexus.Graphics.Vulkan;

public sealed record ShaderDefinition : IGraphicsResource
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ShaderSource Vert { get; }
    public ShaderSource Frag { get; }
    public ShaderContract ShaderContract { get; }
    public ShaderStageEnum Stage { get; }

    public ShaderDefinition(
        string name,
        ShaderSource vert,
        ShaderSource frag,
        ShaderStageEnum shaderStage
    )
    {
        Id = new IdentityHashBuilder(nameof(ShaderDefinition))
            .Add(name)
            .Add(vert.Id)
            .Add(frag.Id)
            .Compute();

        Name = name;
        Vert = vert;
        Frag = frag;
        Stage = shaderStage;
    }
}
