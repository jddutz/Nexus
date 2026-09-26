using System.Text;
using Nexus.Assets.Fonts;
using Nexus.Graphics.Components;

namespace Nexus.Graphics.Text;

/// <summary>Renders a group of MSDF glyphs from one texture atlas as quad instances.</summary>
public sealed class TextSpan : IDrawable, IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>()
        + sizeof(float);

    private static ulong _nextId;
    private readonly DrawableId _id = new(Interlocked.Increment(ref _nextId));
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Matrix4X4<float> _view = Matrix4X4<float>.Identity;
    private ulong _renderLayerMask = ulong.MaxValue;
    private string _text;
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    /// <summary>Initializes a text span with its text and rendering style.</summary>
    /// <param name="style">The font and visual data used to render the text.</param>
    /// <param name="text">The text represented by the span.</param>
    public TextSpan(ITextStyle style, string text)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(text);

        Style = style;
        _text = text;
    }

    /// <inheritdoc/>
    public event EventHandler? RenderLayerChanged;

    /// <inheritdoc/>
    event EventHandler? IDrawable.MeshChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public event EventHandler? TextureChanged;

    /// <inheritdoc/>
    public event EventHandler? InstanceDataChanged;

    /// <inheritdoc/>
    public event EventHandler? UniformDataChanged;

    /// <inheritdoc/>
    event EventHandler? IDrawable.ShaderChanged
    {
        add { }
        remove { }
    }

    /// <summary>Gets or sets the mask of render layers in which this span participates.</summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (_renderLayerMask == value)
                return;

            _renderLayerMask = value;
            RenderLayerChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the font and visual data used to render this span.</summary>
    public ITextStyle Style { get; }

    /// <summary>Gets or sets the text represented by this span.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value)
                return;

            _text = value;
            InstanceDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the shared textured-quad mesh geometry used by the glyphs.</summary>
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

    /// <summary>Gets the texture atlas sampled by the glyphs.</summary>
    public ITexture Texture => Style.Texture;

    /// <summary>Gets the sampling behavior used when sampling the texture atlas.</summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _samplingBehavior;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_samplingBehavior == value)
                return;

            _samplingBehavior = value;
            TextureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the MSDF text vertex shader contract.</summary>
    public VertexShader VertexShader => BuiltInShaders.MsdfTextVertexShader;

    /// <summary>Gets the tessellation-control shader contract, which is not used.</summary>
    public IShaderContract? TessellationControlShader => null;

    /// <summary>Gets the tessellation-evaluation shader contract, which is not used.</summary>
    public IShaderContract? TessellationEvalShader => null;

    /// <summary>Gets the geometry shader contract, which is not used.</summary>
    public IShaderContract? GeometryShader => null;

    /// <summary>Gets the MSDF text fragment shader contract.</summary>
    public FragmentShader FragmentShader => BuiltInShaders.MsdfTextFragmentShader;

    /// <summary>Gets the stable identifier of this span.</summary>
    DrawableId IDrawable.Id => _id;

    /// <summary>Gets the number of glyph instances in this span.</summary>
    ulong IDrawable.InstanceCount => checked((ulong)BuildGlyphs().Count);

    /// <summary>Gets or sets the local transform applied to this span's glyphs.</summary>
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

    /// <summary>Gets or sets the view matrix supplied to the vertex shader contract.</summary>
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

    /// <summary>Gets the first glyph tint for the mesh-instance compatibility contract.</summary>
    public Color Color => Style.Color;

    /// <summary>Gets the packed uniform data required by the textured-quad shader.</summary>
    /// <param name="layout">The requested uniform layout.</param>
    /// <returns>The view matrix.</returns>
    public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException(
                "The uniform layout must contain one 64-byte View input.",
                nameof(layout)
            );

        var data = new byte[64];
        MemoryMarshal.Write(data.AsSpan(), in _view);
        return data;
    }

    /// <summary>Gets the packed instance data for every glyph in this span.</summary>
    /// <param name="layout">The instance layout required by the vertex shader.</param>
    /// <returns>One packed record for each glyph.</returns>
    public ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Sum(input => input.Size) != InstanceDataSize)
            throw new ArgumentException(
                "The instance layout stride does not match the text glyph data.",
                nameof(layout)
            );

        var glyphs = BuildGlyphs();
        var data = new byte[checked(glyphs.Count * InstanceDataSize)];
        var textureRegionOffset = System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>();
        var colorOffset =
            textureRegionOffset + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>();
        var msdfDistanceRangeOffset = colorOffset + Marshal.SizeOf<Color>();
        var scale = Style.FontMetrics.EmSize == 0 ? 1.0 : Style.Size / Style.FontMetrics.EmSize;
        var baselineOffset = (float)(Style.FontMetrics.Ascender * scale);
        var diagnosticGlyphIndex = glyphs.FindIndex(item => item.Glyph.Codepoint == 'H');
        if (diagnosticGlyphIndex < 0 && glyphs.Count > 0)
            diagnosticGlyphIndex = 0;

        for (var index = 0; index < glyphs.Count; index++)
        {
            var destination = data.AsSpan(index * InstanceDataSize, InstanceDataSize);
            var glyph = glyphs[index].Glyph;
            var spanTransform = TransformationMatrix;
            var transformationMatrix =
                CreateTransformation(glyph, glyphs[index].X, baselineOffset) * spanTransform;
            var textureRegion = new Vector4D<float>(
                (float)(glyph.AtlasBounds.Left / Texture.Width),
                (float)(glyph.AtlasBounds.Bottom / Texture.Height),
                (float)((glyph.AtlasBounds.Right - glyph.AtlasBounds.Left) / Texture.Width),
                (float)((glyph.AtlasBounds.Top - glyph.AtlasBounds.Bottom) / Texture.Height)
            );
            var layoutLeft = glyphs[index].X + (float)(glyph.PlaneBounds.Left * scale);
            var layoutRight = glyphs[index].X + (float)(glyph.PlaneBounds.Right * scale);
            var layoutTop = baselineOffset - (float)(glyph.PlaneBounds.Top * scale);
            var layoutBottom = baselineOffset - (float)(glyph.PlaneBounds.Bottom * scale);
            var screenBaselineX = TransformPoint(glyphs[index].X, baselineOffset, spanTransform).X;
            var screenBaselineY = TransformPoint(glyphs[index].X, baselineOffset, spanTransform).Y;
            var topLeft = TransformPoint(layoutLeft, layoutTop, spanTransform);
            var topRight = TransformPoint(layoutRight, layoutTop, spanTransform);
            var bottomLeft = TransformPoint(layoutLeft, layoutBottom, spanTransform);
            var bottomRight = TransformPoint(layoutRight, layoutBottom, spanTransform);
            var screenLeft = MathF.Min(
                MathF.Min(topLeft.X, topRight.X),
                MathF.Min(bottomLeft.X, bottomRight.X)
            );
            var screenRight = MathF.Max(
                MathF.Max(topLeft.X, topRight.X),
                MathF.Max(bottomLeft.X, bottomRight.X)
            );
            var screenTop = MathF.Min(
                MathF.Min(topLeft.Y, topRight.Y),
                MathF.Min(bottomLeft.Y, bottomRight.Y)
            );
            var screenBottom = MathF.Max(
                MathF.Max(topLeft.Y, topRight.Y),
                MathF.Max(bottomLeft.Y, bottomRight.Y)
            );
            if (index == diagnosticGlyphIndex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Text glyph instance. Glyph={new Rune(glyph.Codepoint)}, PlaneBounds(L={glyph.PlaneBounds.Left}, R={glyph.PlaneBounds.Right}, B={glyph.PlaneBounds.Bottom}, T={glyph.PlaneBounds.Top}), ScreenBounds(L={screenLeft}, R={screenRight}, T={screenTop}, B={screenBottom}), AtlasBounds(L={glyph.AtlasBounds.Left}, R={glyph.AtlasBounds.Right}, B={glyph.AtlasBounds.Bottom}, T={glyph.AtlasBounds.Top}), AtlasSize(W={Texture.Width}, H={Texture.Height}), UvRect={textureRegion}, Tint={Style.Color}, InstanceTransform={transformationMatrix}"
                );
            }
            var color = Style.Color;
            MemoryMarshal.Write(destination, in transformationMatrix);
            MemoryMarshal.Write(destination[textureRegionOffset..], in textureRegion);
            MemoryMarshal.Write(destination[colorOffset..], in color);
            var msdfDistanceRange = (float)Style.Msdf.DistanceRange;
            MemoryMarshal.Write(destination[msdfDistanceRangeOffset..], in msdfDistanceRange);
        }

        return data;
    }

    /// <summary>Transforms a span-layout point by the span's local transformation matrix.</summary>
    /// <param name="x">The point's horizontal coordinate.</param>
    /// <param name="y">The point's vertical coordinate.</param>
    /// <param name="transformation">The span transformation matrix.</param>
    /// <returns>The transformed point.</returns>
    private static Vector2D<float> TransformPoint(
        float x,
        float y,
        Matrix4X4<float> transformation
    ) =>
        new(
            x * transformation.M11 + y * transformation.M21 + transformation.M41,
            x * transformation.M12 + y * transformation.M22 + transformation.M42
        );

    private List<(FontGlyph Glyph, float X)> BuildGlyphs()
    {
        var glyphs = new List<(FontGlyph Glyph, float X)>();
        var penX = 0.0;
        var scale = Style.FontMetrics.EmSize == 0 ? 1.0 : Style.Size / Style.FontMetrics.EmSize;
        var previousCodepoint = -1;

        foreach (var rune in Text.EnumerateRunes())
        {
            var codepoint = rune.Value;
            if (!Style.Glyphs.TryGetValue(codepoint, out var glyph))
            {
                // Missing glyphs have no layout metrics, so they do not advance or break kerning.
                continue;
            }

            if (
                previousCodepoint >= 0
                && Style.Kerning.TryGetValue((previousCodepoint, codepoint), out var adjustment)
            )
                penX += adjustment * scale;

            if (
                glyph.PlaneBounds.Right > glyph.PlaneBounds.Left
                && glyph.PlaneBounds.Top > glyph.PlaneBounds.Bottom
            )
                glyphs.Add((glyph, (float)penX));

            penX += glyph.Advance * scale;
            previousCodepoint = codepoint;
        }

        return glyphs;
    }

    /// <summary>Creates the glyph instance transform in top-left, Y-down layout coordinates.</summary>
    /// <param name="glyph">The glyph metrics defining the quad bounds.</param>
    /// <param name="penX">The glyph's horizontal pen position.</param>
    /// <param name="baselineOffset">The scaled baseline distance below the line-box origin.</param>
    /// <returns>The transform placing the glyph quad relative to the span origin.</returns>
    private Matrix4X4<float> CreateTransformation(FontGlyph glyph, float penX, float baselineOffset)
    {
        var scale = Style.FontMetrics.EmSize == 0 ? 1.0 : Style.Size / Style.FontMetrics.EmSize;
        var width = (float)((glyph.PlaneBounds.Right - glyph.PlaneBounds.Left) * scale);
        var height = (float)((glyph.PlaneBounds.Top - glyph.PlaneBounds.Bottom) * scale);
        var centerX =
            penX + (float)((glyph.PlaneBounds.Left + glyph.PlaneBounds.Right) * scale / 2);
        var centerY =
            baselineOffset
            - (float)((glyph.PlaneBounds.Bottom + glyph.PlaneBounds.Top) * scale / 2);

        return Matrix4X4.CreateScale(width, height, 1f)
            * Matrix4X4.CreateTranslation(centerX, centerY, 0f);
    }

    /// <inheritdoc/>
    ulong IDrawable.RenderLayerMask => RenderLayerMask;
}
