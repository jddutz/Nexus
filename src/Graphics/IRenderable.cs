/// <summary>
/// Defines the geometry, resource, instance, and shader data required to render an object.
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// Gets the unique identifier of the renderable.
    /// </summary>
    GraphicsId Id { get; }

    /// <summary>
    /// Gets the vertex data used to define the renderable's geometry.
    /// </summary>
    IVertexDataSource Vertices { get; }

    /// <summary>
    /// Gets the texture resource used by the renderable.
    /// </summary>
    ITexture Texture { get; }

    /// <summary>
    /// Gets the per-instance data used when rendering the renderable.
    /// </summary>
    IInstanceDataSource Instances { get; }

    /// <summary>
    /// Gets the packed uniform data required by the specified shader inputs.
    /// </summary>
    /// <param name="layout">The ordered uniform inputs required by a shader contract.</param>
    /// <returns>The packed uniform-buffer data.</returns>
    ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout);

    /// <summary>
    /// Gets the vertex shader contract used to render the renderable.
    /// </summary>
    VertexShader? VertexShader { get; }

    /// <summary>
    /// Gets the tessellation-control shader contract used to render the renderable.
    /// </summary>
    IShaderContract? TessellationControlShader { get; }

    /// <summary>
    /// Gets the tessellation-evaluation shader contract used to render the renderable.
    /// </summary>
    IShaderContract? TessellationEvalShader { get; }

    /// <summary>
    /// Gets the geometry shader contract used to render the renderable.
    /// </summary>
    IShaderContract? GeometryShader { get; }

    /// <summary>
    /// Gets the fragment shader contract used to render the renderable.
    /// </summary>
    FragmentShader? FragmentShader { get; }

    /// <summary>
    /// Gets the compute shader contract used by the renderable.
    /// </summary>
    IShaderContract? ComputeShader { get; }
}
