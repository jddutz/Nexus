namespace Nexus.Graphics.Shaders;

/// <summary>Describes a shader input and its required size.</summary>
/// <param name="Semantic">The semantic identifier of the input.</param>
/// <param name="Size">The size of the input in bytes.</param>
public sealed record ShaderInput(int Semantic, uint Size);

/// <summary>Describes a stage-specific shader and its input requirements.</summary>
public interface IShaderContract
{
    /// <summary>Gets the stable identifier of the shader contract.</summary>
    GraphicsId Id { get; }

    /// <summary>Gets the display name of the shader.</summary>
    string Name { get; }

    /// <summary>Gets the source file name used to compile the shader.</summary>
    string SourceFileName { get; }

    /// <summary>Gets the shader stage.</summary>
    ShaderStageEnum Stage { get; }

    /// <summary>Gets the vertex format consumed by the shader.</summary>
    VertexFormat VertexFormat { get; }

    /// <summary>Gets the uniform inputs required by the shader.</summary>
    ShaderInput[] UniformLayout { get; }
}
