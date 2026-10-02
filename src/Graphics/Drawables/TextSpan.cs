namespace Nexus.Graphics.Text;

using Nexus.Assets.Fonts;

/// <summary>
/// Renders prepared glyph instances that share one text rendering style. Instance positions are
/// baseline origins in GUI coordinates, where +X points right and +Y points down.
/// </summary>
public sealed class TextSpan : IDrawable
{
    private const int InstanceDataSize = 100;
    private (FontGlyph Glyph, Vector2D<float> Position, Color Color)[] _instances;
    private ulong _renderLayerMask = ulong.MaxValue;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;

    /// <summary>
    /// Initializes a span with the shared style and prepared glyph instances.
    /// </summary>
    /// <param name="textStyle">The shared font, size, atlas, and shader settings.</param>
    /// <param name="instances">The glyph metrics, GUI baseline positions, and per-instance colors.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TextSpan(
        ITextStyle textStyle,
        IReadOnlyList<(FontGlyph Glyph, Vector2D<float> Position, Color Color)> instances
    )
    {
        ArgumentNullException.ThrowIfNull(textStyle);
        ArgumentNullException.ThrowIfNull(instances);

        TextStyle = textStyle;
        _instances = instances.ToArray();
    }

    /// <summary>Gets the shared style used to render the glyph instances.</summary>
    public ITextStyle TextStyle { get; }

    /// <summary>Gets the prepared glyph instances rendered by this span.</summary>
    public IReadOnlyList<(FontGlyph Glyph, Vector2D<float> Position, Color Color)> Instances =>
        _instances;

    /// <inheritdoc />
    public DrawableId Id { get; } = DrawableId.New();

    /// <inheritdoc />
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set => _renderLayerMask = value;
    }

    /// <inheritdoc />
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

    /// <inheritdoc />
    public ITexture Texture => TextStyle.Texture;

    /// <inheritdoc />
    public ulong InstanceCount => checked((ulong)_instances.Length);

    /// <summary>Gets the combined glyph bounds in span-local coordinates.</summary>
    public Rectangle<float> LayoutBounds
    {
        get
        {
            if (_instances.Length == 0)
                return new Rectangle<float>(0f, 0f, 0f, 0f);

            var left = float.PositiveInfinity;
            var top = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            var bottom = float.NegativeInfinity;
            var scale = (float)(TextStyle.Size / TextStyle.FontMetrics.EmSize);
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

    /// <summary>Gets or sets the local transform applied to glyph instances.</summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set
        {
            if (_transformationMatrix == value)
                return;
            _transformationMatrix = value;
            InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the view matrix packed into uniform data.</summary>
    public Matrix4X4<float> View
    {
        get => _view;
        set
        {
            if (_view == value)
                return;
            _view = value;
            UniformDataChanged?.Invoke(this, EventArgs.Empty);
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

    // Required by IDrawable and IObservable; this span is immutable after construction.
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
        if (start > InstanceCount || count > InstanceCount - start)
            throw new ArgumentOutOfRangeException(nameof(count));

        ValidateInstanceLayout(layout);
        var requiredBytes = checked((ulong)InstanceDataSize * count);
        if ((ulong)target.Length < requiredBytes)
            throw new ArgumentException("The target span is too small.", nameof(target));

        var scale = checked((float)(TextStyle.Size / TextStyle.FontMetrics.EmSize));
        var textureWidth = TextStyle.Texture.Width;
        var textureHeight = TextStyle.Texture.Height;
        var distanceRange = checked((float)TextStyle.Msdf.DistanceRange);

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
            MemoryMarshal.Write(targetRecord[80..], in instance.Color);
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

        var view = View;
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
