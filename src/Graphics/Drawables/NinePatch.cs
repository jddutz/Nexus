namespace Nexus.Graphics.Drawables;

/// <summary>Renders a resizable texture region as nine quad instances.</summary>
public partial class NinePatch : IDrawable
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
    private Vector4D<float> _sourceBorders;

    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;

    /// <summary>Initializes a drawable for a nine-patch texture region.</summary>
    /// <param name="texture">The texture to render.</param>
    public NinePatch(ITexture texture) =>
        _texture = texture ?? throw new ArgumentNullException(nameof(texture));

    /// <inheritdoc />
    public ulong InstanceCount => 9;

    /// <inheritdoc />
    public IShaderContract? TessellationControlShader { get; } = null;

    /// <inheritdoc />
    public IShaderContract? TessellationEvalShader { get; } = null;

    /// <inheritdoc />
    public IShaderContract? GeometryShader { get; } = null;

    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    // Required by IDrawable; this drawable's uniform data is immutable and cannot change.
#pragma warning disable CS0067
    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;
#pragma warning restore CS0067

    /// <summary>Raises instance-data invalidation after geometry changes.</summary>
    protected virtual partial void AfterDestinationChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the texture region changes.</summary>
    protected virtual partial void AfterTexCoordChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the texture changes.</summary>
    protected virtual partial void AfterTextureChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the source borders change.</summary>
    protected virtual partial void AfterSourceBordersChanges() =>
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
        if (start > InstanceCount || count > InstanceCount - start)
            throw new ArgumentOutOfRangeException(nameof(count));

        ValidateInstanceLayout(layout);
        var requiredBytes = checked((ulong)InstanceDataSize * count);
        if ((ulong)target.Length < requiredBytes)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var textureWidth = Texture.Width;
        var textureHeight = Texture.Height;
        var sourceLeft = SourceBorders.X / textureWidth;
        var sourceTop = SourceBorders.Y / textureHeight;
        var sourceRight = SourceBorders.Z / textureWidth;
        var sourceBottom = SourceBorders.W / textureHeight;
        var destinationLeft = FitBorders(
            SourceBorders.X,
            SourceBorders.Z,
            Destination.Size.X,
            out var destinationRight
        );
        var destinationTop = FitBorders(
            SourceBorders.Y,
            SourceBorders.W,
            Destination.Size.Y,
            out var destinationBottom
        );
        var destinationWidths = new[]
        {
            destinationLeft,
            Destination.Size.X - destinationLeft - destinationRight,
            destinationRight,
        };
        var destinationHeights = new[]
        {
            destinationTop,
            Destination.Size.Y - destinationTop - destinationBottom,
            destinationBottom,
        };
        var sourceWidths = new[] { sourceLeft, TexCoord.Z - sourceLeft - sourceRight, sourceRight };
        var sourceHeights = new[]
        {
            sourceTop,
            TexCoord.W - sourceTop - sourceBottom,
            sourceBottom,
        };

        for (var offset = 0UL; offset < count; offset++)
        {
            var instanceIndex = checked((int)(start + offset));
            var column = instanceIndex % 3;
            var row = instanceIndex / 3;
            var x =
                column == 0 ? 0f : destinationWidths[0] + (column == 2 ? destinationWidths[1] : 0f);
            var y = row == 0 ? 0f : destinationHeights[0] + (row == 2 ? destinationHeights[1] : 0f);
            var u =
                TexCoord.X
                + (column == 0 ? 0f : sourceWidths[0] + (column == 2 ? sourceWidths[1] : 0f));
            var v =
                TexCoord.Y
                + (row == 0 ? 0f : sourceHeights[0] + (row == 2 ? sourceHeights[1] : 0f));
            var transform = Matrix4X4<float>.Identity;
            transform.M11 = destinationWidths[column];
            transform.M22 = destinationHeights[row];
            transform.M41 = Destination.Origin.X + x;
            transform.M42 = Destination.Origin.Y + y;
            var texCoord = new Vector4D<float>(u, v, sourceWidths[column], sourceHeights[row]);
            var targetRecord = target.Slice(
                checked((int)(offset * InstanceDataSize)),
                InstanceDataSize
            );
            MemoryMarshal.Write(targetRecord, in transform);
            MemoryMarshal.Write(targetRecord[64..], in texCoord);
            var color = Color;
            MemoryMarshal.Write(targetRecord[80..], in color);
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

    /// <summary>
    /// Fits opposing borders within a destination extent by proportionally compressing them
    /// when their combined size exceeds the extent, leaving no center space in that case.
    /// </summary>
    /// <param name="leading">The leading border width.</param>
    /// <param name="trailing">The trailing border width.</param>
    /// <param name="extent">The destination extent.</param>
    /// <param name="fittedTrailing">The fitted trailing border width.</param>
    /// <returns>The fitted leading border width.</returns>
    private static float FitBorders(
        float leading,
        float trailing,
        float extent,
        out float fittedTrailing
    )
    {
        var total = leading + trailing;
        var scale = total > extent && total > 0f ? extent / total : 1f;
        fittedTrailing = trailing * scale;
        return leading * scale;
    }

    /// <summary>Validates the instance layout used by textured quad shaders.</summary>
    /// <param name="layout">The shader instance layout.</param>
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
