namespace Nexus.Graphics.Drawables;

using System.Runtime.Versioning;
using Nexus.Assets.Fonts;

/// <summary>
/// Renders prepared glyph instances that share one atlas and MSDF configuration. Instance
/// positions are baseline origins in GUI coordinates, where +X points right and +Y points down.
/// </summary>
public partial class TextSpan : IDrawable
{
    private const int InstanceDataSize = 100;
    private readonly List<GlyphInstance> _instances = [];

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <summary>Gets or sets the drawable's position in render order.</summary>
    [Observable(PublicSetter = true)]
    private int _drawOrder;

    /// <summary>Replaces one prepared glyph instance and notifies instance-data observers.</summary>
    /// <param name="index">The zero-based index of the instance to replace.</param>
    /// <param name="instance">The immutable glyph metrics, position, and color to render.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the collection.</exception>
    public void SetInstance(int index, GlyphInstance instance)
    {
        if ((uint)index >= (uint)_instances.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_instances[index] == instance)
            return;

        _instances[index] = instance;
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Replaces all prepared glyph instances and notifies instance-data observers.</summary>
    /// <param name="instances">The immutable glyph metrics, positions, and colors to render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instances"/> is null.</exception>
    public void SetInstances(IReadOnlyList<GlyphInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        var replacement = instances.ToList();
        if (_instances.SequenceEqual(replacement))
            return;

        _instances.Clear();
        _instances.AddRange(replacement);
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Appends one prepared glyph instance and notifies instance-data observers.</summary>
    /// <param name="instance">The immutable glyph metrics, position, and color to render.</param>
    public void AddInstance(GlyphInstance instance)
    {
        _instances.Add(instance);
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes the instance at the specified index and notifies observers.</summary>
    /// <param name="index">The zero-based index of the instance to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the collection.</exception>
    public void RemoveInstanceAt(int index)
    {
        if ((uint)index >= (uint)_instances.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _instances.RemoveAt(index);
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes all instances and notifies observers when the span is non-empty.</summary>
    public void ClearInstances()
    {
        if (_instances.Count == 0)
            return;

        _instances.Clear();
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInGeometry.TexturedQuadOffset;

    /// <summary>Gets or sets the atlas texture containing the glyph images.</summary>
    [Observable(PublicSetter = true)]
    private ITexture _texture = BuiltInTextures.Invalid;

    /// <summary>Gets or sets the scale converting glyph plane bounds into rendering units.</summary>
    [Observable(PublicSetter = true)]
    private float _glyphScale = 1f;

    /// <summary>Gets or sets the MSDF distance range encoded in atlas pixels.</summary>
    [Observable(PublicSetter = true)]
    private float _distanceRange = 4f;

    /// <inheritdoc />
    public ulong InstanceCount => checked((ulong)_instances.Count);

    /// <inheritdoc />
    public ISamplingBehavior SamplingBehavior { get; } = SamplingBehaviors.Smooth;

    /// <inheritdoc />
    public VertexShader? VertexShader { get; } = BuiltInShaders.MsdfTextVertexShader;

    /// <inheritdoc />
    public IShaderContract? TessellationControlShader { get; } = null;

    /// <inheritdoc />
    public IShaderContract? TessellationEvalShader { get; } = null;

    /// <inheritdoc />
    public IShaderContract? GeometryShader { get; } = null;

    /// <inheritdoc />
    public FragmentShader? FragmentShader { get; } = BuiltInShaders.MsdfTextFragmentShader;

    // Required by IDrawable and IObservable.
#pragma warning disable CS0067
    /// <inheritdoc />
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc />
    public event EventHandler? UniformDataChanged;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;
#pragma warning restore CS0067

    /// <summary>Raises instance-data invalidation after the atlas texture changes.</summary>
    protected virtual partial void AfterTextureChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the glyph scale changes.</summary>
    protected virtual partial void AfterGlyphScaleChanges() =>
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises instance-data invalidation after the MSDF distance range changes.</summary>
    protected virtual partial void AfterDistanceRangeChanges() =>
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

        var textureWidth = Texture.Width;
        var textureHeight = Texture.Height;
        var scale = GlyphScale;
        var distanceRange = DistanceRange;

        for (var offset = 0UL; offset < count; offset++)
        {
            var instance = _instances[checked((int)(start + offset))];
            var planeBounds = instance.Glyph.PlaneBounds;
            var atlasBounds = instance.Glyph.AtlasBounds;
            var transform = Matrix4X4<float>.Identity;
            transform.M11 = checked((float)(planeBounds.Right - planeBounds.Left)) * scale;
            transform.M22 = checked((float)(planeBounds.Top - planeBounds.Bottom)) * scale;
            transform.M41 = instance.Position.X + checked((float)planeBounds.Left) * scale;
            transform.M42 = instance.Position.Y - checked((float)planeBounds.Top) * scale;
            transform.M43 = DrawOrder;

            var texCoord = new Vector4D<float>(
                checked((float)(atlasBounds.Left / textureWidth)),
                checked((float)((textureHeight - atlasBounds.Top) / textureHeight)),
                checked((float)((atlasBounds.Right - atlasBounds.Left) / textureWidth)),
                checked((float)((atlasBounds.Top - atlasBounds.Bottom) / textureHeight))
            );
            var targetRecord = target.Slice(
                checked((int)(offset * InstanceDataSize)),
                InstanceDataSize
            );
            MemoryMarshal.Write(targetRecord, in transform);
            MemoryMarshal.Write(targetRecord[64..], in texCoord);
            var color = instance.Color;
            MemoryMarshal.Write(targetRecord[80..], in color);
            MemoryMarshal.Write(targetRecord[96..], in distanceRange);
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

    /// <summary>Validates the instance layout required by the MSDF text shader.</summary>
    /// <param name="layout">The shader instance layout.</param>
    private static void ValidateInstanceLayout(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (
            layout.Length != 4
            || layout[0] is not { Semantic: InputSemantics.Transform, Size: 64 }
            || layout[1] is not { Semantic: InputSemantics.TextureRegion, Size: 16 }
            || layout[2] is not { Semantic: InputSemantics.Color, Size: 16 }
            || layout[3] is not { Semantic: InputSemantics.MsdfDistanceRange, Size: 4 }
        )
            throw new ArgumentException(
                "The instance layout must contain Transform, TextureRegion, Color, and MsdfDistanceRange inputs.",
                nameof(layout)
            );
    }
}
