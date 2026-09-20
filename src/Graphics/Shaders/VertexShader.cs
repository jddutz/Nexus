namespace Nexus.Graphics.Shaders;

/// <summary>Describes the inputs and topology required by a vertex shader.</summary>
public sealed record VertexShader : IShaderContract
{
    /// <summary>Gets the stable identifier derived from the shader contract.</summary>
    public ResourceId Id { get; }

    /// <summary>Gets the display name of the shader.</summary>
    public string Name { get; }

    /// <summary>Gets the source file name used to compile the shader.</summary>
    public string SourceFileName { get; }

    /// <summary>Gets the shader stage.</summary>
    public ShaderStageEnum Stage { get; }

    /// <summary>Gets the vertex format consumed by the shader.</summary>
    public VertexFormat VertexFormat { get; }

    /// <summary>Gets the uniform inputs required by the shader.</summary>
    public ShaderInput[] UniformLayout { get; }

    /// <summary>Gets the primitive topology emitted by the shader.</summary>
    public PrimitiveTopologyEnum Topology { get; }

    /// <summary>Gets the per-instance inputs required by the shader.</summary>
    public ShaderInput[] InstanceLayout { get; }

    /// <summary>Creates a vertex shader contract.</summary>
    /// <param name="name">The display name of the shader.</param>
    /// <param name="sourceFileName">The source file name used to compile the shader.</param>
    /// <param name="topology">The primitive topology emitted by the shader.</param>
    /// <param name="vertexFormat">The vertex format consumed by the shader.</param>
    /// <param name="uniformLayout">The uniform inputs required by the shader.</param>
    /// <param name="instanceLayout">The per-instance inputs required by the shader.</param>
    public VertexShader(
        string name,
        string sourceFileName,
        PrimitiveTopologyEnum topology,
        VertexFormat vertexFormat,
        ShaderInput[] uniformLayout,
        ShaderInput[] instanceLayout
    )
    {
        Name = name;
        SourceFileName = sourceFileName;
        Stage = ShaderStageEnum.Vertex;
        VertexFormat = vertexFormat;
        UniformLayout = uniformLayout;
        Topology = topology;
        InstanceLayout = instanceLayout;

        var hash = new IdentityHashBuilder(nameof(VertexShader))
            .Add(Name)
            .Add(SourceFileName)
            .Add((int)Stage)
            .Add(VertexFormat.Id)
            .Add((int)Topology);

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
