namespace Nexus.Graphics.Drawables;

/// <summary>
/// Defines the geometry, resource, instance, and shader data required to render an object.
/// </summary>
public interface IDrawable : IObservable
{
    /// <summary>
    /// Gets the unique identifier of the drawable.
    /// </summary>
    DrawableId Id { get; }

    /// <summary>
    /// Occurs when the drawable's instance data changes.
    /// </summary>
    event EventHandler? InstanceDataChanged;

    /// <summary>
    /// Occurs when the drawable's uniform data changes.
    /// </summary>
    event EventHandler? UniformDataChanged;

    /// <summary>
    /// Gets the mask identifying the render layers on which the drawable is visible.
    /// </summary>
    ulong RenderLayerMask { get; }

    /// <summary>
    /// Gets or sets the drawable's position in render order. Lower values are rendered first.
    /// </summary>
    int DrawOrder { get; set; }

    /// <summary>
    /// Gets the mesh rendered by the drawable.
    /// </summary>
    Mesh Mesh { get; }

    /// <summary>
    /// Gets the texture resource used by the drawable.
    /// </summary>
    ITexture Texture { get; }

    /// <summary>
    /// Gets the number of instances represented by the drawable.
    /// </summary>
    ulong InstanceCount { get; }

    /// <summary>
    /// Gets the sampling behavior used when sampling the drawable's texture.
    /// </summary>
    ISamplingBehavior SamplingBehavior { get; }

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

    /// <summary>
    /// Writes packed instance data required by the specified shader inputs.
    /// </summary>
    /// <param name="start">The zero-based index of the first instance to write.</param>
    /// <param name="count">The number of instances to write.</param>
    /// <param name="layout">The ordered instance inputs required by a shader contract.</param>
    /// <param name="target">The destination buffer for the packed instance data.</param>
    void WriteInstanceDataTo(ulong start, ulong count, ShaderInput[] layout, Span<byte> target);

    /// <summary>
    /// Writes packed uniform data required by the specified shader inputs.
    /// </summary>
    /// <param name="start">The zero-based index of the uniform block to write.</param>
    /// <param name="count">The number of uniform blocks to write.</param>
    /// <param name="layout">The ordered uniform inputs required by a shader contract.</param>
    /// <param name="target">The destination buffer for the packed uniform data.</param>
    void WriteUniformDataTo(ulong start, ulong count, ShaderInput[] layout, Span<byte> target);
}
