namespace Nexus.Graphics.Drawables;

using System.Collections.ObjectModel;

using Nexus.Assets.Fonts;

/// <summary>
/// Renders prepared glyph instances that share one text rendering style. Instance positions are
/// baseline origins in GUI coordinates, where +X points right and +Y points down.
/// </summary>
public partial class TextSpan : IDrawable
{
    private const int InstanceDataSize = 100;
    private GlyphInstance[] _instances;
    private ReadOnlyCollection<GlyphInstance> _instancesView;

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <summary>Initializes a span with the shared style and prepared glyph instances.</summary>
    /// <param name="textStyle">The shared font, size, atlas, and shader settings.</param>
    /// <param name="instances">The immutable glyph metrics, positions, and colors to render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instances"/> is null.</exception>
    public TextSpan(ITextStyle? textStyle, IReadOnlyList<GlyphInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        TextStyle = textStyle;
        _instances = instances.ToArray();
        _instancesView = Array.AsReadOnly(_instances);
    }

    /// <summary>Gets the shared style used to render the glyph instances.</summary>
    public ITextStyle? TextStyle { get; }

    /// <summary>Gets the prepared glyph instances rendered by this span.</summary>
    public IReadOnlyList<GlyphInstance> Instances => _instancesView;

    /// <summary>Replaces one prepared glyph instance and notifies instance-data observers.</summary>
    /// <param name="index">The zero-based index of the instance to replace.</param>
    /// <param name="instance">The immutable glyph metrics, position, and color to render.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the collection.</exception>
    public void SetInstance(int index, GlyphInstance instance)
    {
        if ((uint)index >= (uint)_instances.Length)
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
        var replacement = instances.ToArray();
        ReplaceInstances(replacement);
    }

    /// <summary>Appends one prepared glyph instance and notifies instance-data observers.</summary>
    /// <param name="instance">The immutable glyph metrics, position, and color to render.</param>
    public void AddInstance(GlyphInstance instance)
    {
        var replacement = new GlyphInstance[checked(_instances.Length + 1)];
        Array.Copy(_instances, replacement, _instances.Length);
        replacement[^1] = instance;
        ReplaceInstances(replacement);
    }

    /// <summary>Removes the instance at the specified index and notifies observers.</summary>
    /// <param name="index">The zero-based index of the instance to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the collection.</exception>
    public void RemoveInstanceAt(int index)
    {
        if ((uint)index >= (uint)_instances.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var replacement = new GlyphInstance[_instances.Length - 1];
        Array.Copy(_instances, 0, replacement, 0, index);
        Array.Copy(
            _instances,
            index + 1,
            replacement,
            index,
            _instances.Length - index - 1
        );
        ReplaceInstances(replacement);
    }

    /// <summary>Removes all instances and notifies observers when the span is non-empty.</summary>
    public void ClearInstances()
    {
        if (_instances.Length == 0)
            return;

        ReplaceInstances([]);
    }

    /// <summary>Replaces the owned collection when its contents differ.</summary>
    /// <param name="replacement">The new collection owned by the span.</param>
    private void ReplaceInstances(GlyphInstance[] replacement)
    {
        if (_instances.SequenceEqual(replacement))
            return;

        _instances = replacement;
        _instancesView = Array.AsReadOnly(_instances);
        InstanceDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

    /// <inheritdoc />
    public ITexture Texture => TextStyle?.Texture ?? BuiltInTextures.Invalid;

    /// <inheritdoc />
    public ulong InstanceCount => checked((ulong)_instances.Length);

    /// <summary>Gets the combined glyph bounds in span-local coordinates.</summary>
    public Rectangle<float> LayoutBounds
    {
        get
        {
            var textStyle = TextStyle;
            if (_instances.Length == 0 || textStyle is null)
                return new Rectangle<float>(0f, 0f, 0f, 0f);

            var left = float.PositiveInfinity;
            var top = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            var bottom = float.NegativeInfinity;
            var scale = (float)(textStyle.Size / textStyle.FontMetrics.EmSize);
            foreach (var instance in _instances)
            {
                var bounds = instance.Glyph.PlaneBounds;
                left = MathF.Min(left, instance.Position.X + (float)bounds.Left * scale);
                top = MathF.Min(top, instance.Position.Y - (float)bounds.Top * scale);
                right = MathF.Max(right, instance.Position.X + (float)bounds.Right * scale);
                bottom = MathF.Max(bottom, instance.Position.Y - (float)bounds.Bottom * scale);
            }

            return new Rectangle<float>(left, top, right - left, bottom - top);
        }
    }

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

        var textStyle =
            TextStyle
            ?? throw new InvalidOperationException(
                "Cannot write text instance data without a text style."
            );

        ValidateInstanceLayout(layout);
        var requiredBytes = checked((ulong)InstanceDataSize * count);
        if ((ulong)target.Length < requiredBytes)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var scale = checked((float)(textStyle.Size / textStyle.FontMetrics.EmSize));
        var textureWidth = textStyle.Texture.Width;
        var textureHeight = textStyle.Texture.Height;
        var distanceRange = checked((float)textStyle.Msdf.DistanceRange);

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
