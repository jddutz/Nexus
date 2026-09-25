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
    /// Occurs when the drawable's render-layer mask changes.
    /// </summary>
    event EventHandler? RenderLayerChanged;

    /// <summary>
    /// Occurs when the drawable's mesh changes.
    /// </summary>
    event EventHandler? MeshChanged;

    /// <summary>
    /// Occurs when the drawable's texture or sampling behavior changes.
    /// </summary>
    event EventHandler? TextureChanged;

    /// <summary>
    /// Occurs when the drawable's instance data changes.
    /// </summary>
    event EventHandler? InstanceDataChanged;

    /// <summary>
    /// Occurs when the drawable's uniform data changes.
    /// </summary>
    event EventHandler? UniformDataChanged;

    /// <summary>
    /// Occurs when one or more shader contracts used by the drawable change.
    /// </summary>
    event EventHandler? ShaderChanged;

    /// <summary>
    /// Gets the mask identifying the render layers on which the drawable is visible.
    /// </summary>
    ulong RenderLayerMask { get; }

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
    /// Gets the packed instance data required by the specified shader inputs.
    /// </summary>
    /// <param name="layout">The ordered instance inputs required by a shader contract.</param>
    /// <returns>The packed instance-buffer data.</returns>
    ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout);

    /// <summary>
    /// Gets the sampling behavior used when sampling the drawable's texture.
    /// </summary>
    ISamplingBehavior SamplingBehavior { get; }

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
