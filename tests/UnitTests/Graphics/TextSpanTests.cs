using System.Runtime.InteropServices;
using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI.Elements;
using Silk.NET.Maths;
using Tests;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies prepared text drawables and caller-owned glyph serialization.</summary>
public sealed class TextSpanTests
{
    /// <summary>Verifies GUI text contributes a prepared text span.</summary>
    [Fact]
    public void Drawables_returns_prepared_text_span()
    {
        var span = CreateSpan(CreateStyle(), "AB");

        Assert.Equal(BuiltInMesh.TexturedQuadOffset.Id, span.Mesh.Id);
        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(['A', 'B'], span.Instances.Select(instance => instance.Glyph.Codepoint));
    }

    /// <summary>Verifies text and texture drawables share a unique identifier source.</summary>
    [Fact]
    public void Drawable_ids_are_unique_across_text_and_texture_drawables()
    {
        var texture = new Texture("test", 1, 1, [Colors.White]);
        IDrawable[] drawables =
        [
            Assert.Single(new TextureComponent { Texture = texture }.Drawables),
            Assert.Single(new TextureComponent { Texture = texture }.Drawables),
            CreateSpan(CreateStyle(), "A"),
        ];

        Assert.Equal(drawables.Length, drawables.Select(drawable => drawable.Id).Distinct().Count());
    }

    /// <summary>Verifies prepared glyph bounds include their explicit baseline positions.</summary>
    [Fact]
    public void Layout_bounds_include_prepared_glyph_positions_and_plane_bounds()
    {
        var span = CreateSpan(CreateStyle(), "AB");

        Assert.Equal(new Rectangle<float>(0f, 0f, 2f, 1f), span.LayoutBounds);
    }

    /// <summary>Verifies the GUI render-layer mask reaches existing and replacement spans.</summary>
    [Fact]
    public void Render_layer_mask_is_applied_to_existing_and_future_spans()
    {
        var element = new TextElement("A", CreateStyle());
        var first = DrawableTestData.TextDrawable(element);

        element.RenderLayerMask = 2;
        Assert.Equal(2UL, first.RenderLayerMask);
        element.Text = "B";

        var replacement = DrawableTestData.TextDrawable(element);
        Assert.NotSame(first, replacement);
        Assert.Equal(2UL, replacement.RenderLayerMask);
    }

    /// <summary>Verifies GUI placement survives text replacement without inheriting owner transforms.</summary>
    [Fact]
    public void Arrange_preserves_destination_placement_after_owner_and_text_changes()
    {
        var element = new TextElement("AB", CreateStyle());
        var owner = new GameObject2D { Position = new(100f, 200f) };
        owner.AddChild(element);
        element.Arrange(new Rectangle<float>(3f, 4f, 2f, 1f));
        var first = DrawableTestData.TextDrawable(element);

        Assert.Equal(new Vector2D<float>(3f, 5f), first.Instances[0].Position);
        Assert.Equal(new Rectangle<float>(3f, 4f, 2f, 1f), first.LayoutBounds);
        owner.Position = new(300f, 400f);
        Assert.Equal(new Vector2D<float>(3f, 5f), first.Instances[0].Position);

        element.Text = "BA";
        var replacement = DrawableTestData.TextDrawable(element);
        Assert.Equal(new Vector2D<float>(3f, 5f), replacement.Instances[0].Position);
    }

    /// <summary>Verifies re-arrangement retains local bounds and avoids no-op invalidation.</summary>
    [Fact]
    public void Arrange_updates_instances_without_recreating_span()
    {
        var element = new TextElement("A", CreateStyle());
        var span = DrawableTestData.TextDrawable(element);
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;

        element.Arrange(new Rectangle<float>(2f, 3f, 1f, 1f));
        element.Arrange(new Rectangle<float>(2f, 3f, 1f, 1f));

        Assert.Same(span, DrawableTestData.TextDrawable(element));
        Assert.Equal(new Rectangle<float>(2f, 3f, 1f, 1f), span.LayoutBounds);
        Assert.Equal(1, changes);
    }

    /// <summary>Verifies replacing one immutable glyph updates bounds and notifies once.</summary>
    [Fact]
    public void SetInstance_updates_bounds_and_notifies_once()
    {
        var style = CreateStyle();
        var span = CreateSpan(style, "A");
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;
        var replacement = new GlyphInstance(
            style.Glyphs['B'],
            new Vector2D<float>(3f, 2f),
            Colors.WhiteSmoke
        );

        span.SetInstance(0, replacement);

        Assert.Equal(replacement, span.Instances[0]);
        Assert.Equal(new Rectangle<float>(3f, 1f, 1f, 1f), span.LayoutBounds);
        Assert.Equal(1, changes);
    }

    /// <summary>Verifies replacing all instances copies caller data and notifies once.</summary>
    [Fact]
    public void SetInstances_copies_collection_and_notifies_once()
    {
        var style = CreateStyle();
        var span = CreateSpan(style, "A");
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;
        var expected = new GlyphInstance(
            style.Glyphs['B'],
            new Vector2D<float>(2f, 1f),
            Colors.WhiteSmoke
        );
        var source = new List<GlyphInstance> { expected };

        span.SetInstances(source);
        source[0] = new GlyphInstance(style.Glyphs['A'], Vector2D<float>.Zero, Colors.White);

        Assert.Equal(expected, Assert.Single(span.Instances));
        Assert.Equal(new Rectangle<float>(2f, 0f, 1f, 1f), span.LayoutBounds);
        Assert.Equal(1, changes);
    }

    /// <summary>Verifies appending, indexed removal, and clearing update the collection once.</summary>
    [Fact]
    public void Collection_changes_update_count_and_notify_once_per_change()
    {
        var style = CreateStyle();
        var duplicate = new GlyphInstance(
            style.Glyphs['A'],
            Vector2D<float>.Zero,
            Colors.White
        );
        var span = new TextSpan(style, [duplicate, duplicate]);
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;

        span.AddInstance(new GlyphInstance(style.Glyphs['B'], new(1f, 0f), Colors.White));

        Assert.Equal(3UL, span.InstanceCount);
        Assert.Equal(1, changes);

        span.RemoveInstanceAt(0);

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(duplicate, span.Instances[0]);
        Assert.Equal(2, changes);

        span.ClearInstances();
        span.ClearInstances();

        Assert.Empty(span.Instances);
        Assert.Equal(0UL, span.InstanceCount);
        Assert.Equal(new Rectangle<float>(0f, 0f, 0f, 0f), span.LayoutBounds);
        Assert.Equal(3, changes);
    }

    /// <summary>Verifies GUI preparation retains blank-line baseline spacing in one span.</summary>
    [Fact]
    public void Empty_lines_preserve_multiline_spacing()
    {
        var span = CreateSpan(CreateStyle(), "A\n\nB");

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(3f, span.Instances[1].Position.Y);
        Assert.Equal(new Rectangle<float>(0f, 0f, 1f, 3f), span.LayoutBounds);
    }

    /// <summary>Verifies text replacement publishes removal and addition of prepared drawables.</summary>
    [Fact]
    public void Text_replaces_existing_prepared_span()
    {
        var element = new TextElement("A", CreateStyle());
        var graphics = DrawableTestData.TextGraphics(element);
        var previous = Assert.Single(graphics.Drawables);
        var removed = new List<IDrawable>();
        var added = new List<IDrawable>();
        graphics.DrawableRemoved += (_, e) => removed.Add(e.Drawable);
        graphics.DrawableAdded += (_, e) => added.Add(e.Drawable);

        element.Text = "B";

        Assert.Equal("B", element.Text);
        Assert.Equal("B", DrawableTestData.RenderedText(element));
        Assert.Same(previous, Assert.Single(removed));
        Assert.Same(DrawableTestData.TextDrawable(element), Assert.Single(added));
    }

    /// <summary>Verifies GUI source-text changes notify observers, including an empty value.</summary>
    [Fact]
    public void Text_changes_notify_observers_including_empty_text()
    {
        var element = new TextElement(string.Empty, CreateStyle());
        var properties = new List<string>();
        element.PropertyChanged += properties.Add;

        element.Text = "A";
        element.Text = string.Empty;

        Assert.Equal([nameof(TextElement.Text), nameof(TextElement.Text)], properties);
    }

    /// <summary>Verifies empty or unsupported input prepares an empty glyph collection.</summary>
    /// <param name="text">The input without known glyphs.</param>
    [Theory]
    [InlineData(" ")]
    [InlineData("?")]
    public void Text_without_known_glyphs_prepares_empty_span(string text)
    {
        var span = CreateSpan(CreateStyle(), text);

        Assert.Empty(span.Instances);
        Assert.Equal(0UL, span.InstanceCount);
        Assert.Equal(new Rectangle<float>(0f, 0f, 0f, 0f), span.LayoutBounds);
        Assert.Empty(ReadInstances(span).ToArray());
    }

    /// <summary>Verifies an initially empty GUI element does not allocate a text drawable.</summary>
    [Fact]
    public void Initial_empty_text_has_no_drawable()
    {
        var element = new TextElement(string.Empty, CreateStyle());

        Assert.Empty(Assert.Single(element.Components.OfType<IGraphicsComponent>()).Drawables);
    }

    /// <summary>Verifies visible text can be replaced by empty text and then prepared again.</summary>
    [Fact]
    public void Text_transitions_replace_prepared_drawables()
    {
        var element = new TextElement("A", CreateStyle());
        var original = DrawableTestData.TextDrawable(element);

        element.Text = string.Empty;
        var empty = DrawableTestData.TextDrawable(element);
        Assert.NotSame(original, empty);
        Assert.Equal(0UL, empty.InstanceCount);

        element.Text = "B";
        var visible = DrawableTestData.TextDrawable(element);
        Assert.NotSame(empty, visible);
        Assert.Equal(1UL, visible.InstanceCount);
        Assert.Equal("B", DrawableTestData.RenderedText(element));
    }

    /// <summary>Verifies GUI preparation combines multiple baselines in one drawable.</summary>
    [Fact]
    public void Multiline_text_prepares_one_span_with_combined_layout_bounds()
    {
        var span = CreateSpan(CreateStyle(), "AB\nA");

        Assert.Equal(3UL, span.InstanceCount);
        Assert.Equal(new Vector2D<float>(0f, 2f), span.Instances[2].Position);
        Assert.Equal(new Rectangle<float>(0f, 0f, 2f, 2f), span.LayoutBounds);
    }

    /// <summary>Verifies known whitespace retains its advance and a zero-sized glyph record.</summary>
    [Fact]
    public void Prepared_whitespace_advances_following_glyph()
    {
        var style = new TestTextStyle(new Dictionary<int, FontGlyph>
        {
            ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
            ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
            [' '] = new(' ', 1, new(0, 0, 0, 0), new(0, 0, 0, 0)),
        });
        var span = CreateSpan(style, "A B");
        var data = ReadInstances(span);
        var whitespace = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[100..]);
        var last = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[200..]);

        Assert.Equal(3UL, span.InstanceCount);
        Assert.Equal(0f, whitespace.M11);
        Assert.Equal(0f, whitespace.M22);
        Assert.Equal(2f, last.M41);
    }

    /// <summary>Verifies unknown glyphs do not advance the pen or interrupt kerning.</summary>
    [Fact]
    public void Unsupported_glyph_is_skipped_without_affecting_layout()
    {
        var style = new TestTextStyle(
            CreateStyle().Glyphs,
            new Dictionary<(int, int), double> { [('A', 'B')] = -0.25 }
        );
        var span = CreateSpan(style, "A?B");
        var data = ReadInstances(span);
        var second = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[100..]);

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(0.75f, second.M41);
    }

    /// <summary>Verifies each prepared glyph contributes one complete 100-byte instance.</summary>
    [Fact]
    public void WriteInstanceDataTo_packs_all_glyphs()
    {
        var span = CreateSpan(CreateStyle(), "AB");

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(200, ReadInstances(span).Length);
    }

    /// <summary>Verifies a ranged write selects glyphs without overwriting trailing bytes.</summary>
    [Fact]
    public void WriteInstanceDataTo_writes_requested_range_only()
    {
        var span = CreateSpan(CreateStyle(), "AB");
        var data = Enumerable.Repeat((byte)0xCC, 104).ToArray();

        span.WriteInstanceDataTo(1, 1, BuiltInShaders.MsdfTextVertexShader.InstanceLayout, data);

        Assert.Equal(1f, MemoryMarshal.Read<Matrix4X4<float>>(data).M41);
        Assert.Equal(ReadInstances(span).Span[100..].ToArray(), data[..100]);
        Assert.All(data[100..], value => Assert.Equal((byte)0xCC, value));
    }

    /// <summary>Verifies invalid ranges, undersized buffers, and layouts are rejected.</summary>
    [Fact]
    public void WriteInstanceDataTo_validates_range_buffer_and_layout()
    {
        var span = CreateSpan(CreateStyle(), "AB");
        var layout = BuiltInShaders.MsdfTextVertexShader.InstanceLayout;

        Assert.Throws<ArgumentOutOfRangeException>(() => span.WriteInstanceDataTo(3, 0, layout, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => span.WriteInstanceDataTo(1, 2, layout, new byte[200]));
        Assert.Throws<ArgumentException>(() => span.WriteInstanceDataTo(0, 1, layout, new byte[99]));
        Assert.Throws<ArgumentException>(() => span.WriteInstanceDataTo(0, 1, [], new byte[100]));
    }

    /// <summary>Verifies instance serialization requires a text style.</summary>
    [Fact]
    public void WriteInstanceDataTo_withoutTextStyle_throws()
    {
        var glyph = new FontGlyph('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1));
        var span = new TextSpan(null, [new GlyphInstance(glyph, Vector2D<float>.Zero, Colors.White)]);

        Assert.Throws<InvalidOperationException>(() =>
            span.WriteInstanceDataTo(
                0,
                1,
                BuiltInShaders.MsdfTextVertexShader.InstanceLayout,
                new byte[100]
            )
        );
    }

    /// <summary>Verifies a valid zero-count write is a no-op even without a text style.</summary>
    [Fact]
    public void WriteInstanceDataTo_zeroCount_doesNothing()
    {
        var glyph = new FontGlyph('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1));
        var span = new TextSpan(null, [new GlyphInstance(glyph, Vector2D<float>.Zero, Colors.White)]);
        var target = Enumerable.Repeat((byte)0xCC, 8).ToArray();

        span.WriteInstanceDataTo(1, 0, [], target);

        Assert.All(target, value => Assert.Equal((byte)0xCC, value));
    }

    /// <summary>Verifies glyph bounds convert to GUI coordinates independently of atlas sampling.</summary>
    [Fact]
    public void WriteInstanceDataTo_converts_glyph_bounds_and_packs_atlas_region()
    {
        var glyph = new FontGlyph('H', 48, new(-2, -4, 46, 36), new(1, 0, 2, 1));
        var style = new TestTextStyle(
            new Dictionary<int, FontGlyph> { ['H'] = glyph },
            fontMetrics: new(48, 36, -12, 48),
            size: 18
        );
        var span = new TextSpan(style, [new GlyphInstance(glyph, new(0f, 13.5f), Colors.WhiteSmoke)]);
        var data = ReadInstances(span);
        var transform = MemoryMarshal.Read<Matrix4X4<float>>(data.Span);
        var region = MemoryMarshal.Read<Vector4D<float>>(data.Span[64..]);

        Assert.Equal(18f, transform.M11);
        Assert.Equal(15f, transform.M22);
        Assert.Equal(-0.75f, transform.M41);
        Assert.Equal(0f, transform.M42);
        Assert.Equal(17.25f, transform.M41 + transform.M11);
        Assert.Equal(new Vector4D<float>(0.5f, 0f, 0.5f, 1f), region);
        Assert.Equal(Colors.WhiteSmoke, MemoryMarshal.Read<Color>(data.Span[80..]));
    }

    /// <summary>Verifies fractional prepared positions are serialized without pixel snapping.</summary>
    [Fact]
    public void WriteInstanceDataTo_preserves_fractional_prepared_positions()
    {
        var style = CreateStyle();
        var span = new TextSpan(
            style,
            [new GlyphInstance(style.Glyphs['A'], new(0.4f, 3.4f), style.Color)]
        );
        var transform = MemoryMarshal.Read<Matrix4X4<float>>(ReadInstances(span).Span);

        Assert.Equal(1f, transform.M11);
        Assert.Equal(1f, transform.M22);
        Assert.Equal(0.4f, transform.M41);
        Assert.Equal(2.4f, transform.M42);
    }

    /// <summary>Verifies instance records use prepared coordinates and uniforms pack an identity view.</summary>
    [Fact]
    public void Write_data_packs_prepared_instances_and_identity_view_uniform()
    {
        var span = CreateSpan(CreateStyle(), "AB");
        var expectedInstances = ReadInstances(span).ToArray();

        var uniform = DrawableTestData.ReadUniform(span, BuiltInShaders.MsdfTextVertexShader.UniformLayout);

        Assert.Equal(Matrix4X4<float>.Identity, MemoryMarshal.Read<Matrix4X4<float>>(uniform.Span));
        Assert.Equal(expectedInstances, ReadInstances(span).ToArray());
    }

    /// <summary>Verifies generated font metrics and visual settings are retained by the style.</summary>
    [Fact]
    public void TextStyle_uses_generated_font_data_and_visual_settings()
    {
        var glyph = new FontGlyph('A', 12, new(0, 0, 10, 12), new(0, 0, 10, 12));
        var font = new FontBuildResult(
            new FontAtlas(1, 1, [1, 2, 3]), new FontMetrics(48, 36, -12, 48),
            [glyph], [new TextKerningPair('A', 'V', -2)], new MsdfMetadata(4, 48)
        );
        var texture = new Texture("Roboto", 1, 1, [Colors.White]);
        var style = new TextStyle(font, texture, 18, Colors.WhiteSmoke);

        Assert.Same(texture, style.Texture);
        Assert.Equal(glyph, style.Glyphs['A']);
        Assert.Equal(-2, style.Kerning[('A', 'V')]);
        Assert.Equal(new MsdfMetadata(4, 48), style.Msdf);
        Assert.Equal(18, style.Size);
        Assert.Equal(Colors.WhiteSmoke, style.Color);
    }

    /// <summary>Verifies component defaults match the text layout contract.</summary>
    [Fact]
    public void TextComponent_uses_contract_defaults()
    {
        var component = new TextComponent(CreateStyle());

        Assert.Equal(string.Empty, component.Text);
        Assert.Equal(new Rectangle<float>(0f, 0f, 0f, 0f), component.Destination);
        Assert.Equal(Vector2D<float>.Zero, component.Alignment);
        Assert.Null(component.MaximumLines);
        Assert.True(component.Wrap);
        Assert.Equal(ulong.MaxValue, component.RenderLayerMask);
        Assert.Equal(new Rectangle<float>(0f, 0f, 0f, 0f), component.LayoutBounds);
    }

    /// <summary>Verifies text layout inputs reject invalid values.</summary>
    [Fact]
    public void TextComponent_validates_layout_inputs()
    {
        var component = new TextComponent(CreateStyle()) { Text = "A" };
        var removed = 0;
        component.DrawableRemoved += (_, _) => removed++;

        component.Text = null!;
        Assert.Null(component.Text);
        Assert.Empty(component.Drawables);
        Assert.Equal(1, removed);

        component.Text = "A";
        Assert.Single(component.Drawables);
        component.MaximumLines = 0;
        Assert.Empty(component.Drawables);
        component.MaximumLines = 1;
        Assert.Single(component.Drawables);
        component.Alignment = new(float.NaN, 0f);
        Assert.Empty(component.Drawables);
        component.Alignment = Vector2D<float>.Zero;
        Assert.Single(component.Drawables);
        component.Destination = new Rectangle<float>(0f, 0f, -1f, 1f);
        Assert.Empty(component.Drawables);
        component.Destination = new Rectangle<float>(0f, 0f, 1f, 1f);
        Assert.Single(component.Drawables);
        component.Destination = new Rectangle<float>(float.PositiveInfinity, 0f, 1f, 1f);
        Assert.Empty(component.Drawables);
    }

    /// <summary>Verifies measurement accepts unconstrained dimensions without changing drawables.</summary>
    [Fact]
    public void TextComponent_measure_is_side_effect_free_and_accepts_infinity()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 10f, 10f),
            Text = "A",
        };
        var drawable = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        var added = 0;
        var removed = 0;
        component.DrawableAdded += (_, _) => added++;
        component.DrawableRemoved += (_, _) => removed++;

        Assert.Equal(
            new Vector2D<float>(1f, 1f),
            component.Measure(new(float.PositiveInfinity, float.PositiveInfinity))
        );
        Assert.Same(drawable, Assert.Single(component.Drawables));
        Assert.Equal(0, added);
        Assert.Equal(0, removed);
        Assert.Throws<ArgumentOutOfRangeException>(() => component.Measure(new(float.NaN, 1f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            component.Measure(new(1f, float.NegativeInfinity))
        );
    }

    /// <summary>Verifies destination origin and alignment appear in final glyph bounds.</summary>
    [Fact]
    public void TextComponent_layout_bounds_use_destination_coordinates()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(10f, 20f, 8f, 6f),
            Alignment = new Vector2D<float>(0.5f, 0.5f),
            Text = "A",
        };

        Assert.Equal(new Rectangle<float>(13.5f, 22.5f, 1f, 1f), component.LayoutBounds);
    }

    /// <summary>Verifies MSDF shaders and generated distance range are packed per glyph.</summary>
    [Fact]
    public void TextSpan_uses_msdf_shaders_and_packs_distance_range()
    {
        var span = CreateSpan(CreateStyle(), "A");
        var data = ReadInstances(span);

        Assert.Same(BuiltInShaders.MsdfTextVertexShader, span.VertexShader);
        Assert.Same(BuiltInShaders.MsdfTextFragmentShader, span.FragmentShader);
        Assert.Equal(4f, MemoryMarshal.Read<float>(data.Span[96..]));
    }

    /// <summary>Prepares text through the public GUI composition API.</summary>
    /// <param name="style">The font style used for preparation.</param>
    /// <param name="text">The source text.</param>
    /// <returns>The prepared span.</returns>
    private static TextSpan CreateSpan(ITextStyle style, string text) =>
        DrawableTestData.TextDrawable(new TextElement(text, style));

    /// <summary>Serializes every MSDF glyph instance into caller-owned storage.</summary>
    /// <param name="span">The span to serialize.</param>
    /// <returns>The packed glyph records.</returns>
    private static ReadOnlyMemory<byte> ReadInstances(TextSpan span) =>
        DrawableTestData.ReadInstances(span, BuiltInShaders.MsdfTextVertexShader.InstanceLayout);

    /// <summary>Creates deterministic single-cell glyph metrics.</summary>
    /// <returns>The test text style.</returns>
    private static ITextStyle CreateStyle() => new TestTextStyle(new Dictionary<int, FontGlyph>
    {
        ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
        ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
    });

    /// <summary>Provides in-memory font data for prepared-glyph tests.</summary>
    /// <param name="glyphs">The available glyphs.</param>
    /// <param name="kerning">The optional kerning pairs.</param>
    /// <param name="fontMetrics">The optional font metrics.</param>
    /// <param name="size">The requested text size.</param>
    private sealed class TestTextStyle(
        IReadOnlyDictionary<int, FontGlyph> glyphs,
        IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double>? kerning = null,
        FontMetrics? fontMetrics = null,
        double size = 1
    ) : ITextStyle
    {
        /// <inheritdoc />
        public ITexture Texture { get; } = new Texture("atlas", 2, 1, [Colors.White, Colors.White]);
        /// <inheritdoc />
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } = glyphs;
        /// <inheritdoc />
        public FontMetrics FontMetrics { get; } = fontMetrics ?? new(1, 1, 0, 1);
        /// <inheritdoc />
        public MsdfMetadata Msdf { get; } = new(4, 1);
        /// <inheritdoc />
        public IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double> Kerning { get; } =
            kerning ?? new Dictionary<(int, int), double>();
        /// <inheritdoc />
        public Color Color { get; } = Colors.White;
        /// <inheritdoc />
        public double Size { get; } = size;
    }
}
