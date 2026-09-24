using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
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
        var component = new TextComponent { Text = "Hello" };

        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(BuiltInMesh.TexturedQuadOffset.Id, span.Mesh.Id);
    }

    /// <summary>
    /// Verifies that setting text stores the value and replaces existing spans with one text span.
    /// </summary>
    [Fact]
    public void Text_replaces_existing_spans_with_one_span()
    {
        var component = new TextComponent { Text = "Before" };

        component.Text = "Hello";

        Assert.Equal("Hello", component.Text);
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        Assert.Same(Texture.Invalid, span.Texture);
        Assert.Empty(span.Glyphs);
    }

    /// <summary>
    /// Verifies that every glyph contributes one textured-quad instance record.
    /// </summary>
    [Fact]
    public void GetInstanceData_packs_all_glyphs()
    {
        var span = CreateSpan(2);
        var data = span.GetInstanceData(BuiltInShaders.TexturedQuadVertexShader.InstanceLayout);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(192, data.Length);
    }

    /// <summary>
    /// Creates a span with the requested number of glyph records.
    /// </summary>
    /// <param name="count">The number of glyph records.</param>
    /// <returns>The configured text span.</returns>
    private static TextSpan CreateSpan(int count)
    {
        var glyphs = Enumerable
            .Range(0, count)
            .Select(index => new TextGlyph(
                Matrix4X4.CreateTranslation(index, 0f, 0f),
                new(index * 0.1f, 0f, 0.1f, 1f),
                Colors.White
            ));

        return new TextSpan(new Texture("atlas", 1, 1, [Colors.White]), glyphs);
    }
}
