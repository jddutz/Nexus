using Nexus.Graphics.Textures;

namespace Nexus.Graphics.Components;

/// <summary>
/// Renders a texture-mapped quad with a per-instance transformation matrix, source rectangle, and tint color.
/// </summary>
public class TexturedQuadRenderer
    : Component,
        IGraphicsComponent,
        IDrawable,
        IInstanceDataSource,
        IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>();

    private ulong _renderLayerMask;
    private Texture? _texture;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Vector4D<float> _textureRegion = new(0f, 0f, 1f, 1f);
    private Color _color = Colors.White;
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

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
    /// Gets or sets the mask of render layers in which this component participates.
    /// </summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set => SetProperty(ref _renderLayerMask, value);
    }

    /// <summary>
    /// Gets the single renderable contribution produced by this component.
    /// </summary>
    public IReadOnlyList<IDrawable> Drawables => [this];

    /// <inheritdoc/>
    DrawableId IDrawable.Id => new(Id.Value);

    /// <inheritdoc/>
    ulong IDrawable.RenderLayerMask => RenderLayerMask;

    Mesh IDrawable.Mesh => Mesh;

    ITexture IDrawable.Texture => _texture ?? global::Nexus.Graphics.Textures.Texture.Invalid;

    IInstanceDataSource IDrawable.Instances => this;

    DrawableId IInstanceDataSource.Id =>
        new IdentityHashBuilder(nameof(TexturedQuadRenderer)).Add(Id).Compute();

    ulong IInstanceDataSource.Count => 1;

    VertexShader? IDrawable.VertexShader => BuiltInShaders.TexturedQuadVertexShader;

    IShaderContract? IDrawable.TessellationControlShader => null;

    IShaderContract? IDrawable.TessellationEvalShader => null;

    IShaderContract? IDrawable.GeometryShader => null;

    FragmentShader? IDrawable.FragmentShader => BuiltInShaders.TexturedQuadFragmentShader;

    /// <summary>Gets the number of packed instance records contributed by this component.</summary>
    public int InstanceCount => 1;

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
    /// Gets or sets the sampling behavior used when sampling <see cref="Texture"/>.
    /// </summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _samplingBehavior;
        set => SetProperty(ref _samplingBehavior, value);
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

    /// <summary>Gets or sets the view matrix supplied to the vertex shader contract.</summary>
    public Matrix4X4<float> View
    {
        get => _view;
        set => SetProperty(ref _view, value);
    }

    ReadOnlyMemory<byte> IDrawable.GetUniformData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException(
                "The uniform layout must contain one 64-byte View input.",
                nameof(layout)
            );

        var data = new byte[64];
        MemoryMarshal.Write(data.AsSpan(), in _view);
        return data;
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

    /// <summary>
    /// Gets or writes the packed instance record at the specified component-local index.
    /// </summary>
    /// <param name="instanceIndex">The zero-based instance index.</param>
    /// <param name="destination">The destination span, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(int instanceIndex, Span<byte> destination)
    {
        if (instanceIndex != 0)
            throw new ArgumentOutOfRangeException(nameof(instanceIndex));

        return GetInstanceData(destination);
    }

    ReadOnlyMemory<byte> IInstanceDataSource.GetInstanceData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Sum(input => input.Size) != InstanceDataSize)
            throw new ArgumentException(
                "The instance layout stride does not match the renderer data.",
                nameof(layout)
            );

        var data = new byte[InstanceDataSize];
        GetInstanceData(data);
        return data;
    }
}
