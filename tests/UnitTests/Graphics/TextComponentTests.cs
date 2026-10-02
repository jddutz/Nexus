using System.Runtime.InteropServices;
using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Game;
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
    /// Verifies text spans and component drawables use the same unique identifier source.
    /// </summary>
    [Fact]
    public void Drawable_ids_are_unique_across_text_and_texture_drawables()
    {
        IDrawable[] drawables =
        [
            Assert.Single(new TextureComponent().Drawables),
            Assert.Single(new TextureComponent().Drawables),
            new TextSpan(CreateStyle(), "A"),
        ];

        Assert.Equal(
            drawables.Length,
            drawables.Select(drawable => drawable.Id).Distinct().Count()
        );
    }

    /// <summary>
    /// Verifies text layout bounds include glyph plane bounds and accumulated advances.
    /// </summary>
    [Fact]
    public void Layout_bounds_include_glyph_advances_and_plane_bounds()
    {
        var span = new TextSpan(CreateStyle(), "AB");

        Assert.Equal(new Rectangle<float>(0f, 0f, 2f, 1f), span.LayoutBounds);
    }

    /// <summary>
    /// Verifies the component's render-layer mask is applied to current and future spans.
    /// </summary>
    [Fact]
    public void Render_layer_mask_is_applied_to_existing_and_future_spans()
    {
        var component = new TextComponent(CreateStyle()) { Text = "A" };
        var firstSpan = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        component.RenderLayerMask = 2;

        Assert.Equal((ulong)2, firstSpan.RenderLayerMask);

        component.Text = "B";

        var secondSpan = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        Assert.Equal((ulong)2, secondSpan.RenderLayerMask);
    }

    /// <summary>
    /// Verifies explicit point placement reaches the text span and every packed glyph instance.
    /// </summary>
    [Fact]
    public void Position_is_applied_to_spans_and_glyph_instances()
    {
        var style = CreateStyle();
        var component = new TextComponent(style)
        {
            Position = new Vector2D<float>(3f, 4f),
            Text = "AB",
        };
        var gameObject = new GameObject2D([component]);
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(
            new Vector2D<float>(3f, 4f),
            new(span.TransformationMatrix.M41, span.TransformationMatrix.M42)
        );

        gameObject.Position = new Vector2D<float>(10f, 20f);

        Assert.Equal(
            new Vector2D<float>(3f, 4f),
            new(span.TransformationMatrix.M41, span.TransformationMatrix.M42)
        );
        var glyphData = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var positionedData = new TextSpan(style, "AB")
        {
            TransformationMatrix = Matrix4X4.CreateTranslation(3f, 4f, 0f),
        }.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var glyphStride = glyphData.Length / 2;

        for (var glyphIndex = 0; glyphIndex < 2; glyphIndex++)
        {
            var offset = glyphIndex * glyphStride;
            var glyphTransform = MemoryMarshal.Read<Matrix4X4<float>>(glyphData.Span[offset..]);
            var positionedGlyphTransform = MemoryMarshal.Read<Matrix4X4<float>>(
                positionedData.Span[offset..]
            );

            Assert.Equal(positionedGlyphTransform, glyphTransform);
        }

        component.Text = "BA";

        var replacementSpan = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        Assert.Equal(
            new Vector2D<float>(3f, 4f),
            new(replacementSpan.TransformationMatrix.M41, replacementSpan.TransformationMatrix.M42)
        );
    }

    /// <summary>
    /// Verifies owner assignment does not alter explicit text placement.
    /// </summary>
    [Fact]
    public void Initialize_preserves_explicit_position_after_game_model_assignment()
    {
        var component = new TextComponent(CreateStyle())
        {
            Position = new Vector2D<float>(10f, 20f),
            Text = "A",
        };
        var gameObject = new GameObject2D([component]) { Position = new Vector2D<float>(10f, 20f) };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));

        Assert.Equal(
            new Vector2D<float>(10f, 20f),
            new(span.TransformationMatrix.M41, span.TransformationMatrix.M42)
        );
        gameObject.Position = new Vector2D<float>(30f, 40f);
        Assert.Equal(
            new Vector2D<float>(10f, 20f),
            new(span.TransformationMatrix.M41, span.TransformationMatrix.M42)
        );
    }

    /// <summary>Verifies moving text preserves its local measurement and drawable identity.</summary>
    [Fact]
    public void Position_changes_update_instances_without_recreating_spans()
    {
        var component = new TextComponent(CreateStyle()) { Text = "A" };
        var span = Assert.IsType<TextSpan>(Assert.Single(component.Drawables));
        var bounds = component.LayoutBounds;
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;

        component.Position = new Vector2D<float>(2f, 3f);
        component.Position = new Vector2D<float>(2f, 3f);

        Assert.Same(span, Assert.Single(component.Drawables));
        Assert.Equal(bounds, component.LayoutBounds);
        Assert.Equal(1, changes);
    }

    /// <summary>Verifies empty lines retain their line-height spacing.</summary>
    [Fact]
    public void Empty_lines_preserve_multiline_spacing()
    {
        var component = new TextComponent(CreateStyle()) { Text = "A\n\nB" };
        var spans = component.Drawables.Cast<TextSpan>().ToArray();

        Assert.Equal(2, spans.Length);
        Assert.Equal(2f, spans[1].TransformationMatrix.M42);
        Assert.Equal(new Rectangle<float>(0f, 0f, 1f, 3f), component.LayoutBounds);
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

    /// <summary>Verifies text changes notify observers when measurement becomes empty.</summary>
    [Fact]
    public void Text_changes_notify_observers_including_empty_text()
    {
        var component = new TextComponent(CreateStyle());
        var changedProperties = new List<string>();
        component.PropertyChanged += propertyName => changedProperties.Add(propertyName);

        component.Text = "A";
        component.Text = string.Empty;

        Assert.Equal([nameof(TextComponent.Text), nameof(TextComponent.Text)], changedProperties);
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
    /// Verifies multiline text uses one span per line and reports combined glyph bounds.
    /// </summary>
    [Fact]
    public void Multiline_text_creates_offset_spans_and_combined_layout_bounds()
    {
        var component = new TextComponent(CreateStyle()) { Text = "AB\nA" };

        var spans = component.Drawables.Cast<TextSpan>().ToArray();

        Assert.Equal(2, spans.Length);
        Assert.Equal("AB", spans[0].Text);
        Assert.Equal("A", spans[1].Text);
        Assert.Equal(1f, spans[1].TransformationMatrix.M42);
        Assert.Equal(new Rectangle<float>(0f, 0f, 2f, 2f), component.LayoutBounds);
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
        var data = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var secondGlyph = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[(data.Length / 2)..]);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(
            Matrix4X4.CreateScale(1f, 1f, 1f) * Matrix4X4.CreateTranslation(2f, 0f, 0f),
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
        var data = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var secondGlyph = MemoryMarshal.Read<Matrix4X4<float>>(data.Span[(data.Length / 2)..]);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(0.75f, secondGlyph.M41);
    }

    /// <summary>
    /// Verifies that every glyph contributes one textured-quad instance record.
    /// </summary>
    [Fact]
    public void GetInstanceData_packs_all_glyphs()
    {
        var span = new TextSpan(CreateStyle(), "AB");
        var data = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);

        Assert.Equal((ulong)2, ((IDrawable)span).InstanceCount);
        Assert.Equal(200, data.Length);
    }

    /// <summary>
    /// Verifies glyph-space bounds are converted to top-left layout coordinates independently of atlas sampling.
    /// </summary>
    [Fact]
    public void GetInstanceData_converts_glyph_bounds_and_packs_atlas_region()
    {
        var glyph = new FontGlyph('H', 48, new(-2, -4, 46, 36), new(1, 0, 2, 1));
        var style = new TestTextStyle(
            new Dictionary<int, FontGlyph> { ['H'] = glyph },
            fontMetrics: new(48, 36, -12, 48),
            size: 18
        );
        var span = new TextSpan(style, "H");

        var data = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var transformation = MemoryMarshal.Read<Matrix4X4<float>>(data.Span);
        var textureRegion = MemoryMarshal.Read<Vector4D<float>>(
            data.Span[System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>()..]
        );

        Assert.Equal(18f, transformation.M11);
        Assert.Equal(15f, transformation.M22);
        Assert.Equal(-0.75f, transformation.M41);
        Assert.Equal(0f, transformation.M42);
        Assert.Equal(17.25f, transformation.M41 + transformation.M11);
        Assert.Equal(15f, transformation.M42 + transformation.M22);
        Assert.Equal(new Vector4D<float>(0.5f, 0f, 0.5f, 1f), textureRegion);
    }

    /// <summary>
    /// Verifies an axis-aligned span origin snaps once without changing glyph dimensions.
    /// </summary>
    [Fact]
    public void GetInstanceData_snaps_axis_aligned_glyph_origin_without_resizing()
    {
        var span = new TextSpan(CreateStyle(), "A")
        {
            TransformationMatrix = Matrix4X4.CreateTranslation(0.4f, 2.4f, 0f),
        };

        var data = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var transformation = MemoryMarshal.Read<Matrix4X4<float>>(data.Span);

        Assert.Equal(1f, transformation.M11);
        Assert.Equal(1f, transformation.M22);
        Assert.Equal(0f, transformation.M41);
        Assert.Equal(2f, transformation.M42);
    }

    /// <summary>Verifies span transform changes notify instance-data observers without no-op invalidation.</summary>
    [Fact]
    public void TransformationMatrix_changes_notify_instance_data_observers()
    {
        var span = new TextSpan(CreateStyle(), "A");
        var changes = 0;
        span.InstanceDataChanged += (_, _) => changes++;

        span.TransformationMatrix = Matrix4X4.CreateTranslation(2f, 3f, 0f);
        span.TransformationMatrix = Matrix4X4.CreateTranslation(2f, 3f, 0f);

        Assert.Equal(1, changes);
    }

    /// <summary>
    /// Verifies each glyph instance contains the span transform and the uniform contains only the view.
    /// </summary>
    [Fact]
    public void GetInstanceData_composes_span_transform_and_uniform_packs_view()
    {
        var view = Matrix4X4.CreateTranslation(-3f, -4f, 0f);
        var span = new TextSpan(CreateStyle(), "AB")
        {
            TransformationMatrix = Matrix4X4.CreateTranslation(100f, 100f, 0f),
            View = view,
        };

        var uniformData = span.GetUniformData(BuiltInShaders.MsdfTextVertexShader.UniformLayout);
        var instanceData = span.GetInstanceData(BuiltInShaders.MsdfTextVertexShader.InstanceLayout);
        var packedView = MemoryMarshal.Read<Matrix4X4<float>>(uniformData.Span);
        var packedFirstGlyphTransform = MemoryMarshal.Read<Matrix4X4<float>>(instanceData.Span);

        Assert.Equal(view, packedView);
        Assert.Equal(
            Matrix4X4.CreateScale(1f, 1f, 1f)
                * Matrix4X4.CreateTranslation(0f, 0f, 0f)
                * span.TransformationMatrix,
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
        Assert.Equal(new MsdfMetadata(4, 48), style.Msdf);
        Assert.Equal(18, style.Size);
        Assert.Equal(Colors.WhiteSmoke, style.Color);
    }

    /// <summary>
    /// Verifies text spans select the MSDF shaders and pack the generated distance range per glyph.
    /// </summary>
    [Fact]
    public void TextSpan_uses_msdf_shaders_and_packs_distance_range()
    {
        var span = new TextSpan(CreateStyle(), "A");

        var data = span.GetInstanceData(span.VertexShader.InstanceLayout);

        Assert.Same(BuiltInShaders.MsdfTextVertexShader, span.VertexShader);
        Assert.Same(BuiltInShaders.MsdfTextFragmentShader, span.FragmentShader);
        Assert.Equal(4f, MemoryMarshal.Read<float>(data.Span[96..]));
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
        IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double>? kerning = null,
        FontMetrics? fontMetrics = null,
        double size = 1
    ) : ITextStyle
    {
        public ITexture Texture { get; } = new Texture("atlas", 2, 1, [Colors.White, Colors.White]);
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } = glyphs;
        public FontMetrics FontMetrics { get; } = fontMetrics ?? new(1, 1, 0, 1);
        public MsdfMetadata Msdf { get; } = new(4, 1);
        public IReadOnlyDictionary<
            (int LeftCodepoint, int RightCodepoint),
            double
        > Kerning { get; } = kerning ?? new Dictionary<(int, int), double>();
        public Color Color { get; } = Colors.White;
        public double Size { get; } = size;
    }
}
