namespace Nexus.Graphics.Shaders;

public sealed record Shader
{
    public ResourceId Id { get; }
    public string Name { get; }
    public string SourceFileName { get; }
    public ShaderStageEnum Stage { get; }
    public PrimitiveTopologyEnum Topology { get; }
    public VertexFormat VertexFormat { get; }

    public Shader(
        string name,
        string sourceFileName,
        ShaderStageEnum stage,
        PrimitiveTopologyEnum topology,
        VertexFormat vertexFormat
    )
    {
        Name = name;
        SourceFileName = sourceFileName;
        Stage = stage;
        Topology = topology;
        VertexFormat = vertexFormat;

        Id = new IdentityHashBuilder(nameof(Shader))
            .Add(Name)
            .Add(SourceFileName)
            .Add((uint)Stage)
            .Add((uint)Topology)
            .Add(VertexFormat.Id)
            .Compute();
    }
}
