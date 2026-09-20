namespace Nexus.Graphics.Shaders;

public record Shader
{
    public ResourceId Id { get; }
    public string Name { get; }
    public string SourceFileName { get; }
    public ShaderStageEnum Stage { get; }
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
        VertexFormat = vertexFormat;

        Id = new IdentityHashBuilder(nameof(Shader)).Add(SourceFileName).Add((uint)Stage).Compute();
    }
}
