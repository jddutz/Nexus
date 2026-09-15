namespace Nexus.Graphics.Shaders;

public sealed record ShaderDescription : IShaderContract
{
    public ResourceId Id { get; }
    public string Name { get; }
    public string Source { get; }
    public ShaderStageEnum Stages { get; }

    public PrimitiveTopologyEnum Topology { get; }
    public VertexDescription VertexDescription { get; }

    public ShaderDescription(
        string name,
        string source,
        ShaderStageEnum stages,
        PrimitiveTopologyEnum topology,
        VertexDescription vertexDescription
    )
    {
        Name = name;
        Source = source;
        Stages = stages;
        Topology = topology;
        VertexDescription = vertexDescription;

        Id = new IdentityHashBuilder(nameof(ShaderDescription))
            .Add(Name)
            .Add(Source)
            .Add((uint)Stages)
            .Add((uint)Topology)
            .Add(VertexDescription.Id)
            .Compute();
    }
}
