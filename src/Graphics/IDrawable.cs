namespace Nexus.Graphics;

/// <summary>
/// Defines the geometry, resource, instance, and shader data required to render an object.
/// </summary>
public interface IDrawable
{
    /// <summary>
    /// Gets the unique identifier of the drawable.
    /// </summary>
    DrawableId Id { get; }

    /// <summary>
    /// Gets the RenderLayers on which the drawable is dreawn.
    /// </summary>
    IEnumerable<RenderLayer> RenderLayers { get; }

    /// <summary>
    /// Gets the mesh rendered by the drawable.
    /// </summary>
    Mesh Mesh { get; }

    /// <summary>
    /// Gets the texture resource used by the drawable.
    /// </summary>
    ITexture Texture { get; }

    /// <summary>
    /// Defines the sampling behavior used when rendering.
    /// </summary>
    public ISamplingBehavior SamplingBehavior { get; }

    /// <summary>
    /// Gets the per-instance data used when rendering the drawable.
    /// </summary>
    IInstanceDataSource Instances { get; }

    /// <summary>
    /// Gets the packed uniform data required by the specified shader inputs.
    /// </summary>
    /// <param name="layout">The ordered uniform inputs required by a shader contract.</param>
    /// <returns>The packed uniform-buffer data.</returns>
    ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout);

    /// <summary>
    /// Gets the vertex shader contract used to render the drawable.
    /// </summary>
    VertexShader? VertexShader { get; }

    /// <summary>
    /// Gets the tessellation-control shader contract used to render the drawable.
    /// </summary>
    IShaderContract? TessellationControlShader { get; }

    /// <summary>
    /// Gets the tessellation-evaluation shader contract used to render the drawable.
    /// </summary>
    IShaderContract? TessellationEvalShader { get; }

    /// <summary>
    /// Gets the geometry shader contract used to render the drawable.
    /// </summary>
    IShaderContract? GeometryShader { get; }

    /// <summary>
    /// Gets the fragment shader contract used to render the drawable.
    /// </summary>
    FragmentShader? FragmentShader { get; }
}
