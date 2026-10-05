namespace Nexus.Graphics.Drawables;

/// <summary>Renders a coherent array of textured tile instances over shared quad geometry.</summary>
public partial class TileMapDrawable : IDrawable
{
    private const int InstanceDataSize = 96;
    private TileMapInstance[] _instances = [];

    /// <summary>Gets or sets the render-layer mask shared by every tile instance.</summary>
    [Observable(Public = true)]
    private ulong _renderLayerMask = RenderLayers.All;

    /// <summary>Gets or sets the drawable's position in render order.</summary>
    [Observable(Public = true)]
    private int _drawOrder;

    /// <summary>Gets or sets the texture sampled by all tile instances.</summary>
    [Observable(Public = true)]
    private ITexture _texture = BuiltInTextures.Invalid;

    /// <summary>Gets or sets the sampling behavior used for the shared texture.</summary>
    [Observable(Public = true)]
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    /// <summary>Gets or sets the vertex shader used for textured tile instances.</summary>
    [Observable(Public = true)]
    private VertexShader? _vertexShader = BuiltInShaders.TexturedQuadVertexShader;

    /// <summary>Gets or sets the fragment shader used for textured tile instances.</summary>
    [Observable(Public = true)]
    private FragmentShader? _fragmentShader = BuiltInShaders.TexturedQuadFragmentShader;

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInGeometry.TexturedQuadOffset;

    /// <inheritdoc />
    public ulong InstanceCount => checked((ulong)Volatile.Read(ref _instances).Length);

    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    // Required by IDrawable; this drawable's uniform data is immutable and cannot change.
#pragma warning disable CS0067
    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;
#pragma warning restore CS0067

    /// <inheritdoc />
    public IShaderContract? TessellationControlShader { get; } = null;

    /// <inheritdoc />
    public IShaderContract? TessellationEvalShader { get; } = null;

    /// <inheritdoc />
    public IShaderContract? GeometryShader { get; } = null;

    /// <summary>Atomically replaces every tile instance and invalidates GPU instance data.</summary>
    /// <param name="instances">The complete instance snapshot to publish.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instances"/> is null.</exception>
    public void SetInstances(IReadOnlyList<TileMapInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        var replacement = instances.ToArray();
        var current = Volatile.Read(ref _instances);
        if (current.AsSpan().SequenceEqual(replacement))
            return;

        Volatile.Write(ref _instances, replacement);
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Invalidates packed instance data when the draw order changes.</summary>
    /// <param name="previousValue">The previous draw order.</param>
    protected virtual partial void AfterDrawOrderChanges(int previousValue) =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Invalidates image resources when the shared texture changes.</summary>
    protected virtual partial void AfterTextureChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void WriteInstanceDataTo(
        ulong start,
        ulong count,
        ShaderInput[] layout,
        Span<byte> target
    )
    {
        var instances = Volatile.Read(ref _instances);
        var instanceCount = checked((ulong)instances.Length);
        if (start > instanceCount || count > instanceCount - start)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return;

        ValidateInstanceLayout(layout);
        var requiredBytes = checked((ulong)InstanceDataSize * count);
        if ((ulong)target.Length < requiredBytes)
            throw new ArgumentException("The target span is too small.", nameof(target));

        for (var offset = 0UL; offset < count; offset++)
        {
            var instance = instances[checked((int)(start + offset))];
            var transform = instance.Transform;
            var textureRegion = instance.TextureRegion;
            var color = instance.Color;
            transform.M43 = DrawOrder;
            var record = target.Slice(checked((int)(offset * InstanceDataSize)), InstanceDataSize);
            MemoryMarshal.Write(record, in transform);
            MemoryMarshal.Write(record[64..], in textureRegion);
            MemoryMarshal.Write(record[80..], in color);
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

    /// <summary>Validates the three packed textured-quad instance inputs.</summary>
    /// <param name="layout">The shader instance layout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="layout"/> is null.</exception>
    /// <exception cref="ArgumentException">The layout does not match textured-quad inputs.</exception>
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

/// <summary>Stores the transform and visual data for one tile-map instance.</summary>
/// <param name="Transform">The combined cell-local and owner world transform.</param>
/// <param name="TextureRegion">The normalized texture region.</param>
/// <param name="Color">The tint applied to the tile.</param>
public readonly record struct TileMapInstance(
    Matrix4X4<float> Transform,
    Vector4D<float> TextureRegion,
    Color Color
);
