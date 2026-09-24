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
        var component = new TextComponent(CreateStyle()) { Text = "Hello" };

        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(BuiltInMesh.TexturedQuadOffset.Id, span.Mesh.Id);
    }

    /// <summary>
    /// Verifies that setting text stores the value and replaces existing spans with one text span.
    /// </summary>
    [Fact]
    public void Text_replaces_existing_spans_with_one_span()
    {
        var component = new TextComponent(CreateStyle()) { Text = "Before" };

        component.Text = "Hello";

        Assert.Equal("Hello", component.Text);
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        Assert.Equal(2u, span.Texture.Width);
        Assert.Equal("Hello", span.Text);
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

    private sealed class TestTextStyle(IReadOnlyDictionary<int, FontGlyph> glyphs) : ITextStyle
    {
        public ITexture Texture { get; } = new Texture("atlas", 2, 1, [Colors.White, Colors.White]);
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } = glyphs;
        public FontMetrics FontMetrics { get; } = new(1, 1, 0, 1);
        public IReadOnlyDictionary<
            (int LeftCodepoint, int RightCodepoint),
            double
        > Kerning { get; } = new Dictionary<(int, int), double>();
        public Color Color { get; } = Colors.White;
        public double Size { get; } = 1;
    }
}
