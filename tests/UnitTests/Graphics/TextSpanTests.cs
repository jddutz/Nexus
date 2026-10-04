using System.Runtime.InteropServices;
using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
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

        Assert.Equal(BuiltInGeometry.TexturedQuadOffset.Id, span.Mesh.Id);
        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(
            [new Vector4D<float>(0f, 0f, 0.5f, 1f), new Vector4D<float>(0.5f, 0f, 0f, 1f)],
            ReadTextureRegions(span)
        );
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

        Assert.Equal(
            drawables.Length,
            drawables.Select(drawable => drawable.Id).Distinct().Count()
        );
    }

    /// <summary>Verifies serialized glyph transforms include their prepared positions.</summary>
    [Fact]
    public void Instance_transforms_include_prepared_glyph_positions()
    {
        var span = CreateSpan(CreateStyle(), "AB");

        Assert.Equal(0f, ReadTransform(span).M41);
        Assert.Equal(1f, ReadTransform(span, 1).M41);
    }

    /// <summary>Verifies the GUI render-layer mask reaches existing and replacement spans.</summary>
    [Fact]
    public void Render_layer_mask_is_applied_to_existing_and_future_spans()
    {
        var element = new TextElement("A", CreateStyle());
        element.Arrange(new Rectangle<float>(0f, 0f, 2f, 1f));
        var first = DrawableTestData.TextDrawable(element);

        element.RenderLayerMask = 2;
        Assert.Equal(2UL, first.RenderLayerMask);
        element.Text = "B";

        var replacement = DrawableTestData.TextDrawable(element);
        Assert.Same(first, replacement);
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

        Assert.Equal(3f, ReadTransform(first).M41);
        Assert.Equal(4f, ReadTransform(first).M42);
        owner.Position = new(300f, 400f);
        Assert.Equal(3f, ReadTransform(first).M41);
        Assert.Equal(4f, ReadTransform(first).M42);

        element.Text = "BA";
        var replacement = DrawableTestData.TextDrawable(element);
        Assert.Equal(3f, ReadTransform(replacement).M41);
        Assert.Equal(4f, ReadTransform(replacement).M42);
    }

    /// <summary>Verifies re-arrangement updates transforms and avoids no-op invalidation.</summary>
    [Fact]
    public void Arrange_updates_instances_without_recreating_span()
    {
        var element = new TextElement("A", CreateStyle());
        element.Arrange(new Rectangle<float>(0f, 0f, 1f, 1f));
        var span = DrawableTestData.TextDrawable(element);
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;

        element.Arrange(new Rectangle<float>(2f, 3f, 1f, 1f));
        element.Arrange(new Rectangle<float>(2f, 3f, 1f, 1f));

        Assert.Same(span, DrawableTestData.TextDrawable(element));
        Assert.Equal(2f, ReadTransform(span).M41);
        Assert.Equal(3f, ReadTransform(span).M42);
        Assert.Equal(1, changes);
    }

    /// <summary>Verifies texture and MSDF property changes notify instance-data observers.</summary>
    [Fact]
    public void Texture_and_msdf_changes_notify_property_and_instance_observers()
    {
        var span = new TextSpan();
        var properties = new List<string>();
        var changes = 0;
        span.PropertyChanged += properties.Add;
        span.InstanceDataChanged += (_, _) => changes++;

        span.Texture = CreateStyle().Texture;
        span.GlyphScale = 2f;
        span.DistanceRange = 3f;

        Assert.Contains(nameof(TextSpan.Texture), properties);
        Assert.Contains(nameof(TextSpan.GlyphScale), properties);
        Assert.Contains(nameof(TextSpan.DistanceRange), properties);
        Assert.Equal(3, changes);
    }

    /// <summary>Verifies replacing one immutable glyph updates instance data and notifies once.</summary>
    [Fact]
    public void SetInstance_updates_instance_data_and_notifies_once()
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

        Assert.Equal(3f, ReadTransform(span).M41);
        Assert.Equal(1f, ReadTransform(span).M42);
        Assert.Equal(Colors.WhiteSmoke, ReadInstanceColor(span));
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

        Assert.Equal(2f, ReadTransform(span).M41);
        Assert.Equal(0f, ReadTransform(span).M42);
        Assert.Equal(Colors.WhiteSmoke, ReadInstanceColor(span));
        Assert.Equal(1, changes);
    }

    /// <summary>Verifies appending, indexed removal, and clearing update the collection once.</summary>
    [Fact]
    public void Collection_changes_update_count_and_notify_once_per_change()
    {
        var style = CreateStyle();
        var duplicate = new GlyphInstance(style.Glyphs['A'], Vector2D<float>.Zero, Colors.White);
        var span = new TextSpan { Texture = style.Texture };
        span.SetInstances([duplicate, duplicate]);
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;

        span.AddInstance(new GlyphInstance(style.Glyphs['B'], new(1f, 0f), Colors.White));

        Assert.Equal(3UL, span.InstanceCount);
        Assert.Equal(1, changes);

        span.RemoveInstanceAt(0);

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(
            [new Vector4D<float>(0f, 0f, 0.5f, 1f), new Vector4D<float>(0.5f, 0f, 0f, 1f)],
            ReadTextureRegions(span)
        );
        Assert.Equal(2, changes);

        span.ClearInstances();
        span.ClearInstances();

        Assert.Equal(0UL, span.InstanceCount);
        Assert.Equal(3, changes);
    }

    /// <summary>Verifies GUI preparation retains blank-line baseline spacing in one span.</summary>
    [Fact]
    public void Empty_lines_preserve_multiline_spacing()
    {
        var span = CreateSpan(CreateStyle(), "A\n\nB");

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(2f, ReadTransform(span, 1).M42);
    }

    /// <summary>Verifies source text changes update the existing prepared drawable.</summary>
    [Fact]
    public void Text_changes_reuse_existing_prepared_span()
    {
        var element = new TextElement("A", CreateStyle());
        element.Arrange(new Rectangle<float>(0f, 0f, 1f, 1f));
        var graphics = DrawableTestData.TextGraphics(element);
        var previous = Assert.Single(graphics.Drawables);
        var removed = new List<IDrawable>();
        var added = new List<IDrawable>();
        graphics.DrawableRemoved += (_, e) => removed.Add(e.Drawable);
        graphics.DrawableAdded += (_, e) => added.Add(e.Drawable);

        element.Text = "B";

        Assert.Equal("B", element.Text);
        Assert.Same(previous, DrawableTestData.TextDrawable(element));
        Assert.Empty(removed);
        Assert.Empty(added);
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

    /// <summary>Verifies unsupported text with no prepared glyphs removes the drawable.</summary>
    /// <param name="text">The input without known glyphs.</param>
    [Theory]
    [InlineData("?")]
    public void Text_without_known_glyphs_removes_drawable(string text)
    {
        var element = new TextElement(text, CreateStyle());
        element.Arrange(new Rectangle<float>(0f, 0f, 2f, 1f));
        var graphics = Assert.Single(element.Components.OfType<IGraphicsComponent>());

        Assert.Empty(graphics.Drawables);
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
        element.Arrange(new Rectangle<float>(0f, 0f, 1f, 1f));
        var original = DrawableTestData.TextDrawable(element);
        var graphics = Assert.Single(element.Components.OfType<IGraphicsComponent>());

        element.Text = string.Empty;
        Assert.Empty(graphics.Drawables);

        element.Text = "B";
        var visible = Assert.IsType<TextSpan>(Assert.Single(graphics.Drawables));
        Assert.NotSame(original, visible);
        Assert.Equal(1UL, visible.InstanceCount);
    }

    /// <summary>Verifies GUI preparation combines multiple baselines in one drawable.</summary>
    [Fact]
    public void Multiline_text_prepares_one_span_with_combined_layout_bounds()
    {
        var span = CreateSpan(CreateStyle(), "AB\nA");

        Assert.Equal(3UL, span.InstanceCount);
        Assert.Equal(1f, ReadTransform(span, 2).M42);
    }

    /// <summary>Verifies known whitespace retains its advance and a zero-sized glyph record.</summary>
    [Fact]
    public void Prepared_whitespace_advances_following_glyph()
    {
        var style = new TestTextStyle(
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
                [' '] = new(' ', 1, new(0, 0, 0, 0), new(0, 0, 0, 0)),
            }
        );
        var span = CreateSpan(style, "A B");
        var data = ReadInstances(span);
        var whitespace = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[100..]);
        var last = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[200..]);

        Assert.Equal(3UL, span.InstanceCount);
        Assert.Equal(0f, whitespace.M11);
        Assert.Equal(0f, whitespace.M22);
        Assert.Equal(2f, last.M41);
    }

    /// <summary>Verifies an unsupported rune resets kerning without advancing the pen.</summary>
    [Fact]
    public void Unsupported_glyph_resets_kerning_without_advancing_pen()
    {
        var style = new TestTextStyle(
            CreateStyle().Glyphs,
            new Dictionary<(int, int), double> { [('A', 'B')] = -0.25 }
        );
        var span = CreateSpan(style, "A?B");
        var data = ReadInstances(span);
        var second = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[100..]);

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(1f, second.M41);
    }

    /// <summary>Verifies automatic wrapping prefers a space before splitting a following word.</summary>
    [Fact]
    public void TextComponent_wraps_at_word_boundary_when_separator_exceeds_width()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 2f, 2f),
            Text = "A B",
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(1f, ReadTransform(span, 1).M42);
    }

    /// <summary>Verifies invalid font scale and line-height metrics are rejected explicitly.</summary>
    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(1d, 0d)]
    public void TextComponent_rejects_invalid_font_metrics(double emSize, double lineHeight)
    {
        var style = CreateStyle(fontMetrics: new FontMetrics(emSize, 1, 0, lineHeight));

        Assert.Throws<InvalidOperationException>(() => new TextComponent(style));
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

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            span.WriteInstanceDataTo(3, 0, layout, [])
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            span.WriteInstanceDataTo(1, 2, layout, new byte[200])
        );
        Assert.Throws<ArgumentException>(() =>
            span.WriteInstanceDataTo(0, 1, layout, new byte[99])
        );
        Assert.Throws<ArgumentException>(() => span.WriteInstanceDataTo(0, 1, [], new byte[100]));
    }

    /// <summary>Verifies instance serialization uses span properties without a full text style.</summary>
    [Fact]
    public void WriteInstanceDataTo_uses_span_properties_without_text_style()
    {
        var glyph = new FontGlyph('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1));
        var span = new TextSpan
        {
            Texture = new Texture("atlas", 1, 1, [Colors.White]),
            GlyphScale = 2f,
            DistanceRange = 3f,
        };
        span.SetInstances([new GlyphInstance(glyph, Vector2D<float>.Zero, Colors.White)]);
        var data = new byte[100];

        span.WriteInstanceDataTo(0, 1, BuiltInShaders.MsdfTextVertexShader.InstanceLayout, data);

        Assert.Equal(2f, MemoryMarshal.Read<Matrix4X4<float>>(data).M11);
        Assert.Equal(3f, MemoryMarshal.Read<float>(data.AsSpan(96)));
    }

    /// <summary>Verifies a valid zero-count write is a no-op without configured span data.</summary>
    [Fact]
    public void WriteInstanceDataTo_zeroCount_doesNothing()
    {
        var glyph = new FontGlyph('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1));
        var span = new TextSpan();
        span.SetInstances([new GlyphInstance(glyph, Vector2D<float>.Zero, Colors.White)]);
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
        var span = new TextSpan
        {
            Texture = style.Texture,
            GlyphScale = (float)(style.Size / style.FontMetrics.EmSize),
            DistanceRange = (float)style.Msdf.DistanceRange,
        };
        span.SetInstances([new GlyphInstance(glyph, new(0f, 13.5f), Colors.WhiteSmoke)]);
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
        var span = new TextSpan { Texture = style.Texture };
        span.SetInstances([new GlyphInstance(style.Glyphs['A'], new(0.4f, 3.4f), Colors.White)]);
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

        var uniform = DrawableTestData.ReadUniform(
            span,
            BuiltInShaders.MsdfTextVertexShader.UniformLayout
        );

        Assert.Equal(Matrix4X4<float>.Identity, MemoryMarshal.Read<Matrix4X4<float>>(uniform.Span));
        Assert.Equal(expectedInstances, ReadInstances(span).ToArray());
    }

    /// <summary>Verifies generated font metrics and visual settings are retained by the style.</summary>
    [Fact]
    public void TextStyle_uses_generated_font_data_and_visual_settings()
    {
        var glyph = new FontGlyph('A', 12, new(0, 0, 10, 12), new(0, 0, 10, 12));
        var font = new FontBuildResult(
            new FontAtlas(1, 1, [1, 2, 3]),
            new FontMetrics(48, 36, -12, 48),
            [glyph],
            [new TextKerningPair('A', 'V', -2)],
            new MsdfMetadata(4, 48)
        );
        var texture = new Texture("Roboto", 1, 1, [Colors.White]);
        var style = new TextStyle(font, texture, 18);

        Assert.Same(texture, style.Texture);
        Assert.Equal(glyph, style.Glyphs['A']);
        Assert.Equal(-2, style.Kerning[('A', 'V')]);
        Assert.Equal(new MsdfMetadata(4, 48), style.Msdf);
        Assert.Equal(18, style.Size);
    }

    /// <summary>Verifies equivalent font data produces stable text style identifiers.</summary>
    [Fact]
    public void TextStyle_computes_id_from_font_output_and_style_settings()
    {
        var glyph = new FontGlyph('A', 12, new(0, 0, 10, 12), new(0, 0, 10, 12));
        var font = new FontBuildResult(
            new FontAtlas(1, 1, [1, 2, 3]),
            new FontMetrics(48, 36, -12, 48),
            [glyph],
            [new TextKerningPair('A', 'V', -2)],
            new MsdfMetadata(4, 48)
        );
        var texture = new Texture("Roboto", 1, 1, [Colors.White]);
        var style = new TextStyle(font, texture, 18);
        var equivalentStyle = new TextStyle(
            new FontBuildResult(
                new FontAtlas(1, 1, [1, 2, 3]),
                new FontMetrics(48, 36, -12, 48),
                [glyph],
                [new TextKerningPair('A', 'V', -2)],
                new MsdfMetadata(4, 48)
            ),
            new Texture("Roboto", 1, 1, [Colors.White]),
            18
        );
        var differentFont = new FontBuildResult(
            new FontAtlas(1, 1, [1, 2, 4]),
            font.Metrics,
            font.Glyphs,
            font.Kerning,
            font.Msdf
        );

        Assert.NotEqual(TextStyleId.Invalid, style.Id);
        Assert.Equal(style.Id, equivalentStyle.Id);
        Assert.NotEqual(style.Id, new TextStyle(differentFont, texture, 18).Id);
        Assert.NotEqual(style.Id, new TextStyle(font, texture, 19).Id);
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
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 2f, 2f),
            Text = "A",
        };
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

    /// <summary>Verifies a one-line limit still fills the first permitted line.</summary>
    [Fact]
    public void TextComponent_maximum_lines_limits_line_creation_not_character_processing()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 4f, 1f),
            MaximumLines = 1,
            Text = "AB",
        };

        Assert.Equal(
            2UL,
            Assert.IsType<TextSpan>(Assert.Single(component.Drawables)).InstanceCount
        );
    }

    /// <summary>Verifies CRLF is treated as one line break and LF is not emitted as text.</summary>
    [Fact]
    public void TextComponent_normalizes_crlf_before_rune_enumeration()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 4f, 2f),
            Text = "A\r\nB",
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(2UL, span.InstanceCount);
        Assert.Equal(1f, ReadTransform(span, 1).M42);
    }

    /// <summary>Verifies zero width remains constrained and non-wrapping text keeps only its prefix.</summary>
    [Fact]
    public void TextComponent_zero_width_and_nonwrapping_width_fit_visible_prefixes()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 1f, 1f),
            Wrap = false,
            Text = "AB",
        };

        Assert.Equal(
            1UL,
            Assert.IsType<TextSpan>(Assert.Single(component.Drawables)).InstanceCount
        );
        Assert.Equal(Vector2D<float>.Zero, component.Measure(new(0f, 1f)));
    }

    /// <summary>Verifies measurement and arrangement retain the same complete lines by height.</summary>
    [Fact]
    public void TextComponent_measure_and_arrangement_apply_the_same_height_fitting()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 4f, 1f),
            Text = "A\nB",
        };

        Assert.Equal(new Vector2D<float>(1f, 1f), component.Measure(new(4f, 1f)));
        Assert.Equal(
            1UL,
            Assert.IsType<TextSpan>(Assert.Single(component.Drawables)).InstanceCount
        );
    }

    /// <summary>Verifies each line aligns against its own measured extent.</summary>
    [Fact]
    public void TextComponent_horizontal_alignment_is_calculated_per_line()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 4f, 2f),
            Alignment = new Vector2D<float>(0.5f, 0f),
            Text = "AB\nA",
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(1f, ReadTransform(span).M41);
        Assert.Equal(2f, ReadTransform(span, 1).M41);
        Assert.Equal(1.5f, ReadTransform(span, 2).M41);
    }

    /// <summary>Verifies vertical alignment uses line boxes and preserves leading empty lines.</summary>
    [Fact]
    public void TextComponent_vertical_alignment_uses_logical_line_boxes()
    {
        var component = new TextComponent(CreateStyle(fontMetrics: new FontMetrics(1, 1, 0, 2)))
        {
            Destination = new Rectangle<float>(3f, 4f, 4f, 5f),
            Alignment = new Vector2D<float>(0f, 0.5f),
            Text = "A",
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(5.5f, component.LayoutBounds.Origin.Y);
        Assert.Equal(5.5f, ReadTransform(span).M42);

        component.Text = "\nA";

        Assert.Equal(6.5f, component.LayoutBounds.Origin.Y);
        Assert.Equal(6.5f, ReadTransform(span).M42);
    }

    /// <summary>Verifies style changes synchronize configuration without replacing the span.</summary>
    [Fact]
    public void TextComponent_style_changes_reuse_and_resynchronize_the_span()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 2f, 2f),
            Text = "A",
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        var replacementStyle = CreateStyle(msdf: new(2.5, 1), size: 2);

        component.RenderLayerMask = 4;
        component.TextStyle = replacementStyle;

        Assert.Same(span, Assert.Single(component.Drawables));
        Assert.Same(replacementStyle.Texture, span.Texture);
        Assert.Equal(2f, span.GlyphScale);
        Assert.Equal(2.5f, span.DistanceRange);
        Assert.Equal(4UL, span.RenderLayerMask);
    }

    /// <summary>Verifies text components propagate draw order to existing and replacement spans.</summary>
    [Fact]
    public void TextComponent_propagates_draw_order_to_spans()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 2f, 2f),
            Text = "A",
            DrawOrder = 6,
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(6, span.DrawOrder);
        Assert.Equal(6f, ReadTransform(span).M43);

        component.DrawOrder = -4;

        Assert.Equal(-4, span.DrawOrder);
        Assert.Equal(-4f, ReadTransform(span).M43);
        component.Text = "B";

        Assert.Equal(-4, Assert.Single(component.Drawables).DrawOrder);
    }

    /// <summary>Verifies measurement ignores destination and alignment validity.</summary>
    [Fact]
    public void TextComponent_measure_validates_only_its_own_inputs()
    {
        var component = new TextComponent(CreateStyle())
        {
            Destination = new Rectangle<float>(0f, 0f, 1f, 1f),
            Text = "A",
        };
        component.Destination = new Rectangle<float>(float.PositiveInfinity, 0f, 1f, 1f);
        component.Alignment = new Vector2D<float>(float.NaN, 0f);

        Assert.Equal(new Vector2D<float>(1f, 1f), component.Measure(new(4f, 4f)));
    }

    /// <summary>Verifies unsupported runes break kerning and zero-area glyphs do not affect visible bounds.</summary>
    [Fact]
    public void TextComponent_unsupported_runes_reset_kerning_and_spaces_have_no_visible_bounds()
    {
        var style = CreateStyle(
            kerning: new Dictionary<(int, int), double> { [('A', 'B')] = -0.5 }
        );
        var component = new TextComponent(style)
        {
            Destination = new Rectangle<float>(0f, 0f, 4f, 1f),
            Text = "A?B",
        };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(1f, ReadTransform(span, 1).M41);
        component.Text = " ";

        Assert.Equal(new Rectangle<float>(0f, 0f, 0f, 0f), component.LayoutBounds);
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

    /// <summary>Verifies component style values reach the span's packed glyph data.</summary>
    [Fact]
    public void TextSpan_uses_msdf_shaders_and_packs_distance_range()
    {
        var span = CreateSpan(
            CreateStyle(msdf: new(2.5, 1), size: 18, fontMetrics: new(48, 36, -12, 48)),
            "A"
        );
        var data = ReadInstances(span);

        Assert.Same(BuiltInShaders.MsdfTextVertexShader, span.VertexShader);
        Assert.Same(BuiltInShaders.MsdfTextFragmentShader, span.FragmentShader);
        Assert.Equal(0.375f, span.GlyphScale);
        Assert.Equal(2.5f, MemoryMarshal.Read<float>(data.Span[96..]));
    }

    /// <summary>Prepares text through the public GUI composition API.</summary>
    /// <param name="style">The font style used for preparation.</param>
    /// <param name="text">The source text.</param>
    /// <returns>The prepared span.</returns>
    private static TextSpan CreateSpan(ITextStyle style, string text)
    {
        var element = new TextElement(text, style)
        {
            HorizontalAlignment = AlignHorizontal.Left,
            VerticalAlignment = AlignVertical.Top,
        };
        element.Arrange(new Rectangle<float>(0f, 0f, 100f, 100f));
        return DrawableTestData.TextDrawable(element);
    }

    /// <summary>Serializes every MSDF glyph instance into caller-owned storage.</summary>
    /// <param name="span">The span to serialize.</param>
    /// <returns>The packed glyph records.</returns>
    private static ReadOnlyMemory<byte> ReadInstances(TextSpan span) =>
        DrawableTestData.ReadInstances(span, BuiltInShaders.MsdfTextVertexShader.InstanceLayout);

    /// <summary>Reads one serialized glyph transform.</summary>
    /// <param name="span">The span supplying glyphs.</param>
    /// <param name="index">The zero-based glyph index.</param>
    /// <returns>The transform packed for the selected glyph.</returns>
    private static Matrix4X4<float> ReadTransform(TextSpan span, int index = 0) =>
        MemoryMarshal.Read<Matrix4X4<float>>(ReadInstances(span).Span[(index * 100)..]);

    /// <summary>Reads the serialized atlas regions for every glyph.</summary>
    /// <param name="span">The span supplying glyphs.</param>
    /// <returns>The packed texture regions.</returns>
    private static Vector4D<float>[] ReadTextureRegions(TextSpan span)
    {
        var data = ReadInstances(span).Span;
        var regions = new Vector4D<float>[checked((int)span.InstanceCount)];
        for (var index = 0; index < regions.Length; index++)
            regions[index] = MemoryMarshal.Read<Vector4D<float>>(data[(index * 100 + 64)..]);
        return regions;
    }

    /// <summary>Reads the color packed for the first glyph.</summary>
    /// <param name="span">The span supplying the glyph.</param>
    /// <returns>The packed glyph color.</returns>
    private static Color ReadInstanceColor(TextSpan span) =>
        MemoryMarshal.Read<Color>(ReadInstances(span).Span[80..]);

    /// <summary>Creates deterministic single-cell glyph metrics.</summary>
    /// <returns>The test text style.</returns>
    private static ITextStyle CreateStyle(
        MsdfMetadata? msdf = null,
        double size = 1,
        FontMetrics? fontMetrics = null,
        IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double>? kerning = null
    ) =>
        new TestTextStyle(
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
                [' '] = new(' ', 1, new(0, 0, 0, 0), new(0, 0, 0, 0)),
            },
            kerning: kerning,
            fontMetrics: fontMetrics,
            size: size,
            msdf: msdf
        );

    /// <summary>Provides in-memory font data for prepared-glyph tests.</summary>
    /// <param name="glyphs">The available glyphs.</param>
    /// <param name="kerning">The optional kerning pairs.</param>
    /// <param name="fontMetrics">The optional font metrics.</param>
    /// <param name="size">The requested text size.</param>
    /// <param name="msdf">The optional MSDF atlas metadata.</param>
    private sealed class TestTextStyle(
        IReadOnlyDictionary<int, FontGlyph> glyphs,
        IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double>? kerning = null,
        FontMetrics? fontMetrics = null,
        double size = 1,
        MsdfMetadata? msdf = null
    ) : ITextStyle
    {
        /// <inheritdoc />
        public TextStyleId Id { get; } = new(1);

        /// <inheritdoc />
        public ITexture Texture { get; } = new Texture("atlas", 2, 1, [Colors.White, Colors.White]);

        /// <inheritdoc />
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } = glyphs;

        /// <inheritdoc />
        public FontMetrics FontMetrics { get; } = fontMetrics ?? new(1, 1, 0, 1);

        /// <inheritdoc />
        public MsdfMetadata Msdf { get; } = msdf ?? new(4, 1);

        /// <inheritdoc />
        public IReadOnlyDictionary<
            (int LeftCodepoint, int RightCodepoint),
            double
        > Kerning { get; } = kerning ?? new Dictionary<(int, int), double>();

        /// <inheritdoc />
        public double Size { get; } = size;
    }
}
