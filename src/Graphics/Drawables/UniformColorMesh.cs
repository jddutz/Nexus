namespace Nexus.Graphics.Drawables;

/// <summary>Renders a custom mesh with one transform and one uniform color.</summary>
public sealed class UniformColorMesh : IDrawable
{
    private const int InstanceDataSize = 80;
    private Mesh _mesh;
    private UniformColorMeshInstance[]? _instances;
    private Matrix4X4<float> _transform = Matrix4X4<float>.Identity;
    private Color _color = Colors.White;

    /// <summary>Creates a uniform-color mesh drawable.</summary>
    /// <param name="mesh">The custom mesh to render.</param>
    public UniformColorMesh(Mesh mesh)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Texture = BuiltInTextures.Uniform;
        SamplingBehavior = SamplingBehaviors.PixelPerfect;
    }

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    /// <inheritdoc />
    public Mesh Mesh
    {
        get => _mesh;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_mesh, value))
                return;
            _mesh = value;
            PropertyChanged?.Invoke(nameof(Mesh));
        }
    }

    /// <summary>Gets or sets the world transform applied to the mesh.</summary>
    public Matrix4X4<float> Transform
    {
        get => _transform;
        set
        {
            _transform = value;
            InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the uniform color applied to the mesh.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (_renderLayerMask == value)
                return;
            _renderLayerMask = value;
            PropertyChanged?.Invoke(nameof(RenderLayerMask));
        }
    }

    private ulong _renderLayerMask = RenderLayers.All;

    /// <inheritdoc />
    public int DrawOrder
    {
        get => _drawOrder;
        set
        {
            if (_drawOrder == value)
                return;
            _drawOrder = value;
            PropertyChanged?.Invoke(nameof(DrawOrder));
        }
    }

    private int _drawOrder;

    /// <inheritdoc />
    public ITexture Texture { get; set; }

    /// <inheritdoc />
    public ulong InstanceCount => (ulong)(_instances?.Length ?? 1);

    /// <inheritdoc />
    public ISamplingBehavior SamplingBehavior { get; }

    /// <inheritdoc />
    public VertexShader? VertexShader { get; set; } =
        BuiltInShaders.UniformColorTriangleListVertexShader;

    /// <inheritdoc />
    public IShaderContract? TessellationControlShader => null;

    /// <inheritdoc />
    public IShaderContract? TessellationEvalShader => null;

    /// <inheritdoc />
    public IShaderContract? GeometryShader => null;

    /// <inheritdoc />
    public FragmentShader? FragmentShader { get; set; } =
        BuiltInShaders.UniformColorFragmentShader;

    // Required by IDrawable and IObservable; this drawable has no mutable uniform block.
#pragma warning disable CS0067
    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;
#pragma warning restore CS0067

    /// <inheritdoc />
    public void WriteInstanceDataTo(
        ulong start,
        ulong count,
        ShaderInput[] layout,
        Span<byte> target
    )
    {
        var instances = _instances;
        var total = (ulong)(instances?.Length ?? 1);
        if (start > total || count > total - start)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (layout.Length != 2
            || layout[0] is not { Semantic: InputSemantics.Transform, Size: 64 }
            || layout[1] is not { Semantic: InputSemantics.Color, Size: 16 })
            throw new ArgumentException("The instance layout must contain Transform and Color.", nameof(layout));
        if ((ulong)target.Length < checked(count * InstanceDataSize))
            throw new ArgumentException("The target span is too small.", nameof(target));
        for (ulong offset = 0; offset < count; offset++)
        {
            var instance = instances is null
                ? new UniformColorMeshInstance(Transform, Color)
                : instances[checked((int)(start + offset))];
            var transform = instance.Transform;
            var color = instance.Color;
            var record = target.Slice(checked((int)(offset * InstanceDataSize)), InstanceDataSize);
            MemoryMarshal.Write(record, in transform);
            MemoryMarshal.Write(record[64..], in color);
        }
    }

    /// <summary>Replaces the mesh instances, or restores single-instance rendering when null.</summary>
    public void SetInstances(IReadOnlyList<UniformColorMeshInstance>? instances)
    {
        var replacement = instances?.ToArray();
        if (_instances is null && replacement is null)
            return;
        if (_instances is not null && replacement is not null
            && _instances.AsSpan().SequenceEqual(replacement))
            return;
        _instances = replacement;
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
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
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException("The uniform layout must contain one View input.", nameof(layout));
        if (target.Length < 64)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var view = Matrix4X4<float>.Identity;
        MemoryMarshal.Write(target, in view);
    }
}

/// <summary>A transform and uniform color for one mesh instance.</summary>
public readonly record struct UniformColorMeshInstance(Matrix4X4<float> Transform, Color Color);
