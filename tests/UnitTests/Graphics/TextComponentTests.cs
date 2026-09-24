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
        var span = CreateSpan(2);
        var component = new TextComponent([span]);

        var drawable = Assert.Single(component.Drawables);

        Assert.Same(span, drawable);
        Assert.Equal(BuiltInMesh.TexturedQuadOffset.Id, span.Mesh.Id);
    }

    /// <summary>
    /// Verifies that every glyph contributes one textured-quad instance record.
    /// </summary>
    [Fact]
    public void GetInstanceData_packs_all_glyphs()
    {
        var span = CreateSpan(2);
        var data = span.GetInstanceData(BuiltInShaders.TexturedQuadVertexShader.InstanceLayout);

        Assert.Equal((ulong)2, ((IInstanceDataSource)span).Count);
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
