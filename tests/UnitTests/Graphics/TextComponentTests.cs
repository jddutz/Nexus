using System.Runtime.InteropServices;
using Nexus.Assets.Fonts;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Silk.NET.Maths;

namespace Nexus.UnitTests.Graphics;

/// <summary>
/// Verifies text span drawable projection and glyph instance packing.
/// </summary>
public sealed class TextComponentTests
{
    /// <summary>
    /// Verifies that text spans are exposed as the component's drawable contributions.
    /// </summary>
    [Fact]
    public void Drawables_returns_text_spans()
    {
        var component = new TextComponent(CreateStyle()) { Text = "AB" };

        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(BuiltInMesh.TexturedQuadOffset.Id, span.Mesh.Id);
    }

    /// <summary>
    /// Verifies that setting text stores the value and replaces existing spans with one text span.
    /// </summary>
    [Fact]
    public void Text_replaces_existing_spans_with_one_span()
    {
        var component = new TextComponent(CreateStyle()) { Text = "A" };
        var previousSpan = Assert.Single(component.Drawables);
        var removed = new List<IDrawable>();
        var added = new List<IDrawable>();
        component.DrawableRemoved += (_, e) => removed.Add(e.Drawable);
        component.DrawableAdded += (_, e) => added.Add(e.Drawable);

        component.Text = "B";

        Assert.Equal("B", component.Text);
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        Assert.Equal(2u, span.Texture.Width);
        Assert.Equal("B", span.Text);
        Assert.Same(previousSpan, Assert.Single(removed));
        Assert.Same(span, Assert.Single(added));
    }

    /// <summary>
    /// Verifies empty and whitespace-only text do not expose drawable spans.
    /// </summary>
    [Fact]
    public void Text_without_renderable_glyphs_has_no_drawables()
    {
        var component = new TextComponent(CreateStyle());
        var removed = new List<IDrawable>();
        var added = new List<IDrawable>();
        component.DrawableRemoved += (_, e) => removed.Add(e.Drawable);
        component.DrawableAdded += (_, e) => added.Add(e.Drawable);

        component.Text = string.Empty;
        component.Text = " ";

        Assert.Empty(component.Drawables);
        Assert.Empty(removed);
        Assert.Empty(added);
    }

    /// <summary>
    /// Verifies visible text creates one drawable containing its renderable glyphs.
    /// </summary>
    [Fact]
    public void Visible_text_creates_one_drawable()
    {
        var component = new TextComponent(CreateStyle()) { Text = "AB" };

        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
    }

    /// <summary>
    /// Verifies changing visible text to empty text removes its drawable.
    /// </summary>
    [Fact]
    public void Visible_text_to_empty_text_removes_drawable()
    {
        var component = new TextComponent(CreateStyle()) { Text = "A" };
        var previousSpan = Assert.Single(component.Drawables);
        var removed = new List<IDrawable>();
        var added = new List<IDrawable>();
        component.DrawableRemoved += (_, e) => removed.Add(e.Drawable);
        component.DrawableAdded += (_, e) => added.Add(e.Drawable);

        component.Text = string.Empty;

        Assert.Empty(component.Drawables);
        Assert.Same(previousSpan, Assert.Single(removed));
        Assert.Empty(added);
    }

    /// <summary>
    /// Verifies changing empty text to visible text adds its drawable.
    /// </summary>
    [Fact]
    public void Empty_text_to_visible_text_adds_drawable()
    {
        var component = new TextComponent(CreateStyle());
        var removed = new List<IDrawable>();
        var added = new List<IDrawable>();
        component.DrawableRemoved += (_, e) => removed.Add(e.Drawable);
        component.DrawableAdded += (_, e) => added.Add(e.Drawable);

        component.Text = "A";

        Assert.Single(component.Drawables);
        Assert.Empty(removed);
        Assert.Same(Assert.Single(component.Drawables), Assert.Single(added));
    }

    /// <summary>
    /// Verifies a layout-only space advances the following glyph without creating an instance.
    /// </summary>
    [Fact]
    public void Layout_whitespace_advances_following_glyph_without_rendering()
    {
        var span = new TextSpan(CreateStyleWithSpace(), "A B");
        var data = span.GetInstanceData(BuiltInShaders.TexturedQuadVertexShader.InstanceLayout);
        var secondGlyph = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[(data.Length / 2)..]);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(
            Matrix4X4.CreateScale(1f, 1f, 1f) * Matrix4X4.CreateTranslation(2.5f, 0.5f, 0f),
            secondGlyph
        );
    }

    /// <summary>
    /// Verifies unsupported glyphs do not produce an empty drawable.
    /// </summary>
    [Fact]
    public void Unsupported_glyph_does_not_create_drawable()
    {
        var component = new TextComponent(CreateStyle()) { Text = "?" };

        Assert.Empty(component.Drawables);
    }

    /// <summary>
    /// Verifies an unsupported code point is skipped without advancing or breaking kerning.
    /// </summary>
    [Fact]
    public void Unsupported_glyph_is_skipped_without_affecting_layout()
    {
        var style = new TestTextStyle(
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
            },
            new Dictionary<(int, int), double> { [('A', 'B')] = -0.25 }
        );
        var span = new TextSpan(style, "A?B");
        var data = span.GetInstanceData(BuiltInShaders.TexturedQuadVertexShader.InstanceLayout);
        var secondGlyph = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[(data.Length / 2)..]);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(1.25f, secondGlyph.M41);
    }

    /// <summary>
    /// Verifies that every glyph contributes one textured-quad instance record.
    /// </summary>
    [Fact]
    public void GetInstanceData_packs_all_glyphs()
    {
        var span = new TextSpan(CreateStyle(), "AB");
        var data = span.GetInstanceData(BuiltInShaders.TexturedQuadVertexShader.InstanceLayout);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(192, data.Length);
    }

    /// <summary>
    /// Verifies the span transform is packed separately from each glyph's local transform.
    /// </summary>
    [Fact]
    public void GetUniformData_packs_span_transform_separately_from_glyph_transforms()
    {
        var span = new TextSpan(CreateStyle(), "AB")
        {
            TransformationMatrix = Matrix4X4.CreateTranslation(10f, 20f, 0f),
        };

        var uniformData = span.GetUniformData(
            BuiltInShaders.TexturedQuadVertexShader.UniformLayout
        );
        var instanceData = span.GetInstanceData(
            BuiltInShaders.TexturedQuadVertexShader.InstanceLayout
        );
        var packedSpanTransform = MemoryMarshal.Read<Matrix4X4<float>>(uniformData.Span);
        var packedFirstGlyphTransform = MemoryMarshal.Read<Matrix4X4<float>>(instanceData.Span);

        Assert.Equal(span.TransformationMatrix, packedSpanTransform);
        Assert.Equal(
            Matrix4X4.CreateScale(1f, 1f, 1f) * Matrix4X4.CreateTranslation(0.5f, 0.5f, 0f),
            packedFirstGlyphTransform
        );
    }

    /// <summary>
    /// Verifies that generated glyph metrics and visual settings are retained by a text style.
    /// </summary>
    [Fact]
    public void TextStyle_uses_generated_font_data_and_visual_settings()
    {
        var glyph = new FontGlyph('A', 12, new(0, 0, 10, 12), new(0, 0, 10, 12));
        var kerning = new TextKerningPair('A', 'V', -2);
        var font = new FontBuildResult(
            new FontAtlas(1, 1, [1, 2, 3]),
            new FontMetrics(48, 36, -12, 48),
            [glyph],
            [kerning],
            new MsdfMetadata(4, 48)
        );
        var texture = new Texture("Roboto", 1, 1, [Colors.White]);
        var style = new TextStyle(font, texture, 18, Colors.WhiteSmoke);

        Assert.Same(texture, style.Texture);
        Assert.Equal(glyph, style.Glyphs['A']);
        Assert.Equal(-2, style.Kerning[('A', 'V')]);
        Assert.Equal(18, style.Size);
        Assert.Equal(Colors.WhiteSmoke, style.Color);
    }

    private static ITextStyle CreateStyle()
    {
        return new TestTextStyle(
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
            }
        );
    }

    private static ITextStyle CreateStyleWithSpace()
    {
        return new TestTextStyle(
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
                [' '] = new(' ', 1, new(0, 0, 0, 0), new(0, 0, 0, 0)),
            }
        );
    }

    private sealed class TestTextStyle(
        IReadOnlyDictionary<int, FontGlyph> glyphs,
        IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double>? kerning = null
    ) : ITextStyle
    {
        public ITexture Texture { get; } = new Texture("atlas", 2, 1, [Colors.White, Colors.White]);
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } = glyphs;
        public FontMetrics FontMetrics { get; } = new(1, 1, 0, 1);
        public IReadOnlyDictionary<
            (int LeftCodepoint, int RightCodepoint),
            double
        > Kerning { get; } = kerning ?? new Dictionary<(int, int), double>();
        public Color Color { get; } = Colors.White;
        public double Size { get; } = 1;
    }
}
