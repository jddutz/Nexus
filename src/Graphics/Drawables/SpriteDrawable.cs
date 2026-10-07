namespace Nexus.Graphics.Drawables;

/// <summary>Renders sprites sharing one atlas, shaders, and draw order.</summary>
public partial class SpriteDrawable : IDrawable
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

    private SpriteInstance[] _instances = [];
    private Matrix4X4<float> _worldTransform = Matrix4X4<float>.Identity;

    /// <inheritdoc />
    public ulong InstanceCount => (ulong)_instances.Length;

    internal void SetInstances(SpriteInstance[] instances, Matrix4X4<float> worldTransform)
    {
        _instances = instances;
        _worldTransform = worldTransform;
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

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
        if ((ulong)target.Length < count * InstanceDataSize)
            throw new ArgumentException("The target span is too small.", nameof(target));
        for (ulong index = 0; index < count; index++)
        {
            var instance = _instances[checked((int)(start + index))];
            var transform = Matrix4X4.CreateScale(instance.Size.X, instance.Size.Y, 1f)
                * Matrix4X4.CreateTranslation(-instance.Anchor.X * instance.Size.X, -instance.Anchor.Y * instance.Size.Y, 0f)
                * instance.Transform * _worldTransform;
            transform.M43 = DrawOrder;
            var texCoord = instance.TexCoord;
            var color = instance.Color;
            var record = target.Slice(checked((int)index * InstanceDataSize), InstanceDataSize);
            MemoryMarshal.Write(record, in transform);
            MemoryMarshal.Write(record[64..], in texCoord);
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
