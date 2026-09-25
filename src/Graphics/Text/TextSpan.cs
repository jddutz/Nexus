using System.Text;
using Nexus.Assets.Fonts;
using Nexus.Graphics.Components;

namespace Nexus.Graphics.Text;

/// <summary>Renders a group of glyphs from one texture atlas as textured-quad instances.</summary>
//
// TODO: Consider dedicated UI/text shaders.
// The textured-quad shaders are sufficient for the initial text rendering path,
// but text should ultimately use an unlit UI-oriented pipeline with appropriate
// transparency/depth behavior. A dedicated fragment shader may also interpret
// MSDF atlas data for improved glyph edge rendering and text effects.
public sealed class TextSpan : IDrawable, IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()
        + System.Runtime.CompilerServices.Unsafe.SizeOf<Vector4D<float>>()
        + Marshal.SizeOf<Color>();

    private static ulong _nextId;
    private readonly DrawableId _id = new(Interlocked.Increment(ref _nextId));
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;

    /// <summary>Initializes a text span with its text and rendering style.</summary>
    /// <param name="style">The font and visual data used to render the text.</param>
    /// <param name="text">The text represented by the span.</param>
    public TextSpan(ITextStyle style, string text)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(text);

        Style = style;
        Text = text;
    }

    /// <summary>Gets or sets the mask of render layers in which this span participates.</summary>
    public ulong RenderLayerMask { get; set; } = ulong.MaxValue;

    /// <summary>Gets the font and visual data used to render this span.</summary>
    public ITextStyle Style { get; }

    /// <summary>Gets or sets the text represented by this span.</summary>
    public string Text { get; set; }

    /// <summary>Gets the shared textured-quad mesh geometry used by the glyphs.</summary>
    public Mesh Mesh { get; } = BuiltInMesh.TexturedQuadOffset;

    /// <summary>Gets the texture atlas sampled by the glyphs.</summary>
    public ITexture Texture => Style.Texture;

    /// <summary>Gets the sampling behavior used when sampling the texture atlas.</summary>
    public ISamplingBehavior SamplingBehavior { get; set; } = SamplingBehaviors.Smooth;

    /// <summary>Gets the textured-quad vertex shader contract.</summary>
    public VertexShader VertexShader => BuiltInShaders.TexturedQuadVertexShader;

    /// <summary>Gets the tessellation-control shader contract, which is not used.</summary>
    public IShaderContract? TessellationControlShader => null;

    /// <summary>Gets the tessellation-evaluation shader contract, which is not used.</summary>
    public IShaderContract? TessellationEvalShader => null;

    /// <summary>Gets the geometry shader contract, which is not used.</summary>
    public IShaderContract? GeometryShader => null;

    /// <summary>Gets the textured-quad fragment shader contract.</summary>
    public FragmentShader FragmentShader => BuiltInShaders.TexturedQuadFragmentShader;

    /// <summary>Gets the stable identifier of this span.</summary>
    DrawableId IDrawable.Id => _id;

    /// <summary>Gets the number of glyph instances in this span.</summary>
    ulong IDrawable.InstanceCount => checked((ulong)BuildGlyphs().Count);

    /// <summary>Gets or sets the local transform applied to this span's glyphs.</summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set => _transformationMatrix = value;
    }

    /// <summary>Gets the first glyph tint for the mesh-instance compatibility contract.</summary>
    public Color Color => Style.Color;

    /// <summary>Gets the packed uniform data required by the textured-quad shader.</summary>
    /// <param name="layout">The requested uniform layout.</param>
    /// <returns>The span-local transform matrix.</returns>
    public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Length != 1 || layout[0] is not { Semantic: InputSemantics.View, Size: 64 })
            throw new ArgumentException(
                "The uniform layout must contain one 64-byte View input.",
                nameof(layout)
            );

        var data = new byte[64];
        var transformationMatrix = TransformationMatrix;
        MemoryMarshal.Write(data.AsSpan(), in transformationMatrix);
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
        for (var index = 0; index < glyphs.Count; index++)
        {
            var destination = data.AsSpan(index * InstanceDataSize, InstanceDataSize);
            var glyph = glyphs[index].Glyph;
            var transformationMatrix = CreateTransformation(glyph, glyphs[index].X);
            var textureRegion = new Vector4D<float>(
                (float)(glyph.AtlasBounds.Left / Texture.Width),
                (float)(glyph.AtlasBounds.Bottom / Texture.Height),
                (float)((glyph.AtlasBounds.Right - glyph.AtlasBounds.Left) / Texture.Width),
                (float)((glyph.AtlasBounds.Top - glyph.AtlasBounds.Bottom) / Texture.Height)
            );
            var color = Style.Color;
            MemoryMarshal.Write(destination, in transformationMatrix);
            MemoryMarshal.Write(destination[textureRegionOffset..], in textureRegion);
            MemoryMarshal.Write(destination[colorOffset..], in color);
        }

        return data;
    }

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
                continue;

            if (
                previousCodepoint >= 0
                && Style.Kerning.TryGetValue((previousCodepoint, codepoint), out var adjustment)
            )
                penX += adjustment * scale;

            glyphs.Add((glyph, (float)penX));
            penX += glyph.Advance * scale;
            previousCodepoint = codepoint;
        }

        return glyphs;
    }

    private Matrix4X4<float> CreateTransformation(FontGlyph glyph, float penX)
    {
        var scale = Style.FontMetrics.EmSize == 0 ? 1.0 : Style.Size / Style.FontMetrics.EmSize;
        var width = (float)((glyph.PlaneBounds.Right - glyph.PlaneBounds.Left) * scale);
        var height = (float)((glyph.PlaneBounds.Top - glyph.PlaneBounds.Bottom) * scale);
        var centerX =
            penX + (float)((glyph.PlaneBounds.Left + glyph.PlaneBounds.Right) * scale / 2);
        var centerY = (float)((glyph.PlaneBounds.Bottom + glyph.PlaneBounds.Top) * scale / 2);
        return Matrix4X4.CreateScale(width, height, 1f)
            * Matrix4X4.CreateTranslation(centerX, centerY, 0f);
    }

    /// <inheritdoc/>
    ulong IDrawable.RenderLayerMask => RenderLayerMask;
}
