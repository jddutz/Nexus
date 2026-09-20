namespace Nexus.Graphics.Shaders;

/// <summary>Describes the inputs and outputs required by a fragment shader.</summary>
public sealed record FragmentShader : IShaderContract
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

    /// <summary>Gets the color format produced by the shader.</summary>
    public ColorFormatEnum ColorFormat { get; }

    /// <summary>Creates a fragment shader contract.</summary>
    /// <param name="name">The display name of the shader.</param>
    /// <param name="sourceFileName">The source file name used to compile the shader.</param>
    /// <param name="vertexFormat">The vertex format consumed by the shader.</param>
    /// <param name="colorFormat">The color format produced by the shader.</param>
    /// <param name="uniformLayout">The uniform inputs required by the shader.</param>
    public FragmentShader(
        string name,
        string sourceFileName,
        VertexFormat vertexFormat,
        ColorFormatEnum colorFormat,
        ShaderInput[] uniformLayout
    )
    {
        Name = name;
        SourceFileName = sourceFileName;
        Stage = ShaderStageEnum.Fragment;
        VertexFormat = vertexFormat;
        UniformLayout = uniformLayout;
        ColorFormat = colorFormat;

        var hash = new IdentityHashBuilder(nameof(FragmentShader))
            .Add(Name)
            .Add(SourceFileName)
            .Add((int)Stage)
            .Add(VertexFormat.Id)
            .Add((int)ColorFormat);

        foreach (var input in UniformLayout)
        {
            hash.Add(input.Semantic).Add(input.Size);
        }

        Id = hash.Compute();
    }
}
