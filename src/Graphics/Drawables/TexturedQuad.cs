namespace Nexus.Graphics.Drawables;

/// <summary>Renders a single quad with a specific region of a given texture.</summary>
public partial class TexturedQuad : IDrawable
{
    private const int InstanceDataSize = 96;

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

    [Observable(PublicSetter = true)]
    private ITexture _texture;

    [Observable(PublicSetter = true)]
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    [Observable(PublicSetter = true)]
    private VertexShader? _vertexShader = BuiltInShaders.TexturedQuadVertexShader;

    [Observable(PublicSetter = true)]
    private FragmentShader? _fragmentShader = BuiltInShaders.TexturedQuadFragmentShader;

    [Observable(PublicSetter = true)]
    private Rectangle<float> _destination = new(0f, 0f, 1f, 1f);

    [Observable(PublicSetter = true)]
    private Vector4D<float> _texCoord = new(0f, 0f, 1f, 1f);

    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;

    /// <summary>Initializes a drawable for the complete texture region.</summary>
    /// <param name="texture">The texture to render.</param>
    public TexturedQuad(ITexture texture) =>
        _texture = texture ?? throw new ArgumentNullException(nameof(texture));

    /// <inheritdoc />
    public ulong InstanceCount => 1;

    public IShaderContract? TessellationControlShader { get; } = null;
    public IShaderContract? TessellationEvalShader { get; } = null;
    public IShaderContract? GeometryShader { get; } = null;

    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;

    /// <summary>Raises instance-data invalidation after the destination changes.</summary>
    protected virtual partial void AfterDestinationChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the texture region changes.</summary>
    protected virtual partial void AfterTexCoordChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the color changes.</summary>
    protected virtual partial void AfterColorChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void WriteInstanceDataTo(
        ulong start,
        ulong count,
        ShaderInput[] layout,
        Span<byte> target
    )
    {
        if (start != 0 || count != 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        ValidateInstanceLayout(layout);
        if (target.Length < InstanceDataSize)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var transform = Matrix4X4<float>.Identity;
        transform.M11 = Destination.Size.X;
        transform.M22 = Destination.Size.Y;
        transform.M41 = Destination.Origin.X;
        transform.M42 = Destination.Origin.Y;
        var texCoord = TexCoord;
        var color = Color;
        MemoryMarshal.Write(target, in transform);
        MemoryMarshal.Write(target[64..], in texCoord);
        MemoryMarshal.Write(target[80..], in color);
    }

    /// <inheritdoc />
    public void WriteUniformDataTo(
        ulong start,
        ulong count,
        ShaderInput[] layout,
        Span<byte> target
    )
    {
        if (start != 0 || count != 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException(
                "The uniform layout must contain one 64-byte View input.",
                nameof(layout)
            );
        if (target.Length < 64)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var view = Matrix4X4<float>.Identity;
        MemoryMarshal.Write(target, in view);
    }

    private static void ValidateInstanceLayout(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (
            layout.Length != 3
            || layout[0] is not { Semantic: InputSemantics.Transform, Size: 64 }
            || layout[1] is not { Semantic: InputSemantics.TextureRegion, Size: 16 }
            || layout[2] is not { Semantic: InputSemantics.Color, Size: 16 }
        )
            throw new ArgumentException(
                "The instance layout must contain Transform, TextureRegion, and Color inputs.",
                nameof(layout)
            );
    }
}
