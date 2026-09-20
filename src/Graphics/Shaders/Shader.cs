namespace Nexus.Graphics.Shaders;

/// <summary>Describes a shader input and its required size.</summary>
public sealed record ShaderInput(int Semantic, uint Size);

/// <summary>Describes a stage-specific shader and its input requirements.</summary>
public record Shader
{
    public ResourceId Id { get; }
    public string Name { get; }
    public string SourceFileName { get; }
    public ShaderStageEnum Stage { get; }
    public VertexFormat VertexFormat { get; }
    public ShaderInput[] UniformLayout { get; }
    public ShaderInput[] InstanceLayout { get; }

    public Shader(
        string name,
        string sourceFileName,
        ShaderStageEnum stage,
        VertexFormat vertexFormat,
        ShaderInput[] uniformInputs,
        ShaderInput[] instanceInputs
    )
    {
        Name = name;
        SourceFileName = sourceFileName;
        Stage = stage;
        VertexFormat = vertexFormat;
        UniformLayout = uniformInputs;
        InstanceLayout = instanceInputs;

        var hash = new IdentityHashBuilder(nameof(Shader)).Add(SourceFileName).Add((uint)Stage);

        foreach (var input in UniformLayout)
        {
            hash.Add(input.Semantic).Add(input.Size);
        }

        foreach (var input in InstanceLayout)
        {
            hash.Add(input.Semantic).Add(input.Size);
        }

        Id = hash.Compute();
    }
}
