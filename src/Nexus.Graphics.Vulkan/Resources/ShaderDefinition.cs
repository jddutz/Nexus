namespace Nexus.Graphics.Vulkan.Resources;

public sealed record ShaderDefinition : IResourceDefinition
{
    public ResourceId Id { get; }
    public string Name { get; }
    public string Source { get; }
    public ShaderStageFlags StageFlags { get; }
    public ShaderContract Contract { get; }

    public ShaderDefinition(
        string name,
        string source,
        ShaderStageFlags stageFlags,
        ShaderContract contract
    )
    {
        Id = new IdentityHashBuilder(nameof(ShaderDefinition))
            .Add(name)
            .Add(source)
            .Add((uint)stageFlags)
            .Add(contract.Id)
            .Compute();

        Name = name;
        Source = source;
        StageFlags = stageFlags;
        Contract = contract;
    }
}
