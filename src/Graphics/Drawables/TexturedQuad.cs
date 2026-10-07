namespace Nexus.Graphics.Drawables;

/// <summary>Renders a single quad with a specific region of a given texture.</summary>
public partial class TexturedQuad : IDrawable
{
    private const int InstanceDataSize = 96;

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = RenderLayers.All;

    /// <summary>Gets or sets the drawable's position in render order.</summary>
    [Observable(PublicSetter = true)]
    private int _drawOrder;

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInGeometry.TexturedQuadOffset;

    [Observable(PublicSetter = true)]
    private ITexture _texture = BuiltInTextures.Invalid;

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

    [Observable(PublicSetter = true)]
    private Nexus.Graphics.ClippingMask _clippingMask;

    [Observable(PublicSetter = true)]
    private float _clippingInset;

    protected virtual void SetClippingInset(float value)
    {
        if (!float.IsFinite(value) || value < 0f || value >= 0.5f)
            throw new ArgumentOutOfRangeException(nameof(value));
        _clippingInset = value;
    }

    protected virtual partial void AfterClippingInsetChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    protected virtual void SetClippingMask(Nexus.Graphics.ClippingMask value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        _clippingMask = value;
    }

    protected virtual partial void AfterClippingMaskChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public ulong InstanceCount => 1;

    public IShaderContract? TessellationControlShader { get; } = null;
    public IShaderContract? TessellationEvalShader { get; } = null;
    public IShaderContract? GeometryShader { get; } = null;

    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    // Required by IDrawable; this drawable's uniform data is immutable and cannot change.
#pragma warning disable CS0067
    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;
#pragma warning restore CS0067

    /// <summary>Raises instance-data invalidation after the destination changes.</summary>
    protected virtual partial void AfterDestinationChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the texture region changes.</summary>
    protected virtual partial void AfterTexCoordChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the color changes.</summary>
    protected virtual partial void AfterColorChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the draw order changes.</summary>
    protected virtual partial void AfterDrawOrderChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void WriteInstanceDataTo(
        ulong start,
        ulong count,
        ShaderInput[] layout,
        Span<byte> target
    )
    {
        // TODO: use layout to determine how the data is written to the buffer

        if (start > InstanceCount || count > InstanceCount - start)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0)
            return;

        ValidateInstanceLayout(layout);
        var size = layout.Sum(input => input.Size);
        if (ClippingMask != Nexus.Graphics.ClippingMask.None && layout.Length != 4)
            throw new InvalidOperationException("Clipping requires the masked image shader instance layout.");
        if (target.Length < size)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var transform = Matrix4X4<float>.Identity;
        transform.M11 = Destination.Size.X;
        transform.M22 = Destination.Size.Y;
        transform.M41 = Destination.Origin.X;
        transform.M42 = Destination.Origin.Y;
        transform.M43 = DrawOrder;
        var texCoord = TexCoord;
        var color = Color;
        MemoryMarshal.Write(target, in transform);
        MemoryMarshal.Write(target[64..], in texCoord);
        MemoryMarshal.Write(target[80..], in color);
        if (layout.Length == 4)
        {
            var mask = new Vector2D<float>((float)ClippingMask, ClippingInset);
            MemoryMarshal.Write(target[96..], in mask);
        }
    }

    /// <inheritdoc />
    public void WriteUniformDataTo(
        ulong start,
        ulong count,
        ShaderInput[] layout,
        Span<byte> target
    )
    {
        // TODO: use layout to determine how the data is written to the buffer

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
            layout.Length is not (3 or 4)
            || layout[0] is not { Semantic: InputSemantics.Transform, Size: 64 }
            || layout[1] is not { Semantic: InputSemantics.TextureRegion, Size: 16 }
            || layout[2] is not { Semantic: InputSemantics.Color, Size: 16 }
            || (layout.Length == 4 && layout[3] is not { Semantic: InputSemantics.ClippingMask, Size: 8 })
        )
            throw new ArgumentException(
                "The instance layout must contain Transform, TextureRegion, and Color inputs.",
                nameof(layout)
            );
    }
}
