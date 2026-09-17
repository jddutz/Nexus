using Nexus.Graphics.Textures;

namespace Nexus.Graphics.Components;

/// <summary>
/// Renders a texture-mapped quad with a per-instance transformation matrix, source rectangle, and tint color.
/// </summary>
public class TexturedQuadRenderer : Component, IRenderableComponent, IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>();

    private HashSet<RenderLayer> _renderLayers = [];
    private Texture? _texture;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Vector4D<float> _textureRegion = new(0f, 0f, 1f, 1f);
    private Color _color = Colors.White;

    /// <summary>
    /// Initializes a textured quad renderer with a corner-pivoted quad mesh.
    /// </summary>
    public TexturedQuadRenderer()
        : this(centered: false) { }

    /// <summary>
    /// Initializes a textured quad renderer with the requested mesh pivot.
    /// </summary>
    /// <param name="centered">
    /// <see langword="true"/> to use a center-pivoted mesh; otherwise, a corner-pivoted mesh.
    /// </param>
    public TexturedQuadRenderer(bool centered)
    {
        Mesh = centered ? BuiltInMesh.TexturedQuadCentered : BuiltInMesh.TexturedQuadOffset;
    }

    /// <summary>
    /// Gets the render layers in which this component participates.
    /// </summary>
    public IEnumerable<RenderLayer> RenderLayers => _renderLayers;

    /// <summary>
    /// Gets the constant quad geometry rendered by this component, pivoted at its center when
    /// <paramref name="centered"/> is <see langword="true"/>, otherwise pivoted at its corner.
    /// </summary>
    public Mesh Mesh { get; }

    /// <summary>
    /// Gets or sets the texture sampled by this component.
    /// </summary>
    public Texture? Texture
    {
        get => _texture;
        set => SetProperty(ref _texture, value);
    }

    /// <summary>
    /// Gets or sets the transformation matrix applied to the quad.
    /// </summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set => SetProperty(ref _transformationMatrix, value);
    }

    /// <summary>
    /// Gets or sets the atlas UV transform (U offset, V offset, U scale, V scale) used to sample <see cref="Texture"/>.
    /// </summary>
    public Vector4D<float> TextureRegion
    {
        get => _textureRegion;
        set => SetProperty(ref _textureRegion, value);
    }

    /// <summary>
    /// Gets or sets the tint color multiplied against the sampled texture color.
    /// </summary>
    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    /// <summary>
    /// Gets the required instance-record size or writes the transform, texture region, and color to a destination span.
    /// </summary>
    /// <param name="destination">The destination for the instance record, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(Span<byte> destination)
    {
        var transformationMatrixSize = System.Runtime.CompilerServices.Unsafe.SizeOf<
            Matrix4X4<float>
        >();
        var textureRegionSize = System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>();

        if (destination.Length < InstanceDataSize)
            return InstanceDataSize;

        var transformationMatrix = TransformationMatrix;
        var textureRegion = TextureRegion;
        var color = Color;
        MemoryMarshal.Write(destination, in transformationMatrix);
        MemoryMarshal.Write(destination[transformationMatrixSize..], in textureRegion);
        MemoryMarshal.Write(
            destination[(transformationMatrixSize + textureRegionSize)..],
            in color
        );

        return InstanceDataSize;
    }
}
