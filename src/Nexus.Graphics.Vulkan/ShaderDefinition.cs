namespace Nexus.Graphics.Vulkan;

public sealed record ShaderDefinition : IGraphicsResource
{
    public ResourceId Id { get; }
    public string Name { get; }
    public ShaderSource Source { get; }
    public ShaderStageFlags StageFlags { get; }
    public ShaderContract Contract { get; }

    public ShaderDefinition(
        string name,
        ShaderSource source,
        ShaderStageFlags stageFlags,
        ShaderContract contract
    )
    {
        Id = new IdentityHashBuilder(nameof(ShaderDefinition))
            .Add(name)
            .Add(source.Id)
            .Add((uint)stageFlags)
            .Add(contract.Id)
            .Compute();

        Name = name;
        Source = source;
        StageFlags = stageFlags;
        Contract = contract;
    }
}
