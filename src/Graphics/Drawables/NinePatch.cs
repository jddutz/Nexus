namespace Nexus.Graphics.Drawables;

/// <summary>Renders a resizable texture region as nine quad instances.</summary>
public partial class NinePatch : IDrawable
{
    private const int InstanceDataSize = 96;

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <summary>Gets or sets the drawable's position in render order.</summary>
    [Observable(PublicSetter = true)]
    private int _drawOrder;

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

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
    private Vector4D<float> _sourceBorders;

    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;

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

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

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
        var requiredBytes = checked((ulong)InstanceDataSize * count);
        if ((ulong)target.Length < requiredBytes)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var texture = Texture ?? BuiltInTextures.Invalid;
        var textureWidth = texture.Width;
        var textureHeight = texture.Height;
        var sourceLeft = SourceBorders.X / textureWidth;
        var sourceTop = SourceBorders.Y / textureHeight;
        var sourceRight = SourceBorders.Z / textureWidth;
        var sourceBottom = SourceBorders.W / textureHeight;
        var horizontalBorders = SourceBorders.X + SourceBorders.Z;
        var verticalBorders = SourceBorders.Y + SourceBorders.W;
        var scale = 1f;
        if (horizontalBorders > 0f)
            scale = MathF.Min(scale, Destination.Size.X / horizontalBorders);
        if (verticalBorders > 0f)
            scale = MathF.Min(scale, Destination.Size.Y / verticalBorders);

        var destinationLeft = SourceBorders.X * scale;
        var destinationTop = SourceBorders.Y * scale;
        var destinationRight = SourceBorders.Z * scale;
        var destinationBottom = SourceBorders.W * scale;
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
            transform.M43 = DrawOrder;
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
