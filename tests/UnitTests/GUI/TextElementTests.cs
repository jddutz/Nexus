using Nexus.Assets.Fonts;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests text measurement, placement, and visual lifecycle for GUI text elements.</summary>
public sealed class TextElementTests
{
    /// <summary>Verifies text wraps to available width and height during measurement.</summary>
    [Fact]
    public void Measure_wrapsTextToAvailableBounds()
    {
        var element = CreateTextElement("AB");

        Assert.Equal(new Vector2D<float>(1f, 2f), element.Measure(new(1f, 2f)));
        Assert.Equal(Vector2D<float>.Zero, element.Measure(Vector2D<float>.Zero));
    }

    /// <summary>Verifies a text element can be initialized before its rendering style is assigned.</summary>
    [Fact]
    public void Parameterless_construction_defers_drawables_until_style_is_assigned()
    {
        var element = new TextElement { Text = "A" };
        var graphics = Assert.Single(element.Components.OfType<IGraphicsComponent>());

        Assert.Equal("A", element.Text);
        Assert.Empty(graphics.Drawables);

        element.Style = new TestTextStyle();

        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(element));
    }

    /// <summary>Verifies horizontal and vertical alignment position the rendered glyph bounds.</summary>
    [Fact]
    public void Arrange_positionsTextUsingConfiguredAlignment()
    {
        var element = CreateTextElement("A");
        var bounds = new Rectangle<float>(10f, 20f, 8f, 6f);

        element.Arrange(bounds);
        Assert.Equal(new Rectangle<float>(13.5f, 22.5f, 1f, 1f), element.Bounds);
        Assert.Equal(new Vector2D<float>(13.5f, 22.5f), element.Position);

        element.HorizontalAlignment = AlignHorizontal.Right;
        element.VerticalAlignment = AlignVertical.Bottom;

        Assert.Equal(new Rectangle<float>(17f, 25f, 1f, 1f), element.Bounds);
        Assert.Equal(new Vector2D<float>(17f, 25f), element.Position);
    }

    /// <summary>Verifies a measured line remains visible when glyphs are shorter than line height.</summary>
    [Fact]
    public void Arrange_keepsTextWhenGlyphHeightIsLessThanLineHeight()
    {
        var element = new TextElement("A", new TestTextStyle(lineHeight: 2));
        var measuredSize = element.Measure(new(10f, 1f));

        element.Arrange(new Rectangle<float>(0f, 0f, measuredSize.X, measuredSize.Y));

        Assert.Equal(new Vector2D<float>(1f, 1f), measuredSize);
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(element));
        Assert.Equal(1f, element.Bounds.Size.Y);
    }

    /// <summary>Verifies hiding removes visuals while text and layout survive recreation.</summary>
    [Fact]
    public void AncestorVisibility_recreatesTextComponentFromRetainedState()
    {
        var parent = new Element();
        var element = CreateTextElement("AB");
        var bounds = new Rectangle<float>(2f, 3f, 8f, 4f);
        element.Arrange(bounds);
        var original = DrawableTestData.TextGraphics(element);
        parent.AddChild(element);

        parent.IsVisible = false;
        Assert.Empty(element.Components);
        Assert.Equal(Vector2D<float>.Zero, element.Measure(new(20f, 20f)));
        element.Text = "BA";

        parent.IsVisible = true;

        var recreated = DrawableTestData.TextGraphics(element);
        Assert.NotNull(recreated);
        Assert.NotSame(original, recreated);
        Assert.Equal(2UL, DrawableTestData.TextInstanceCount(element));
        Assert.Equal(new Vector2D<float>(2f, 1f), element.Measure(new(20f, 20f)));
        Assert.Equal(new Rectangle<float>(5f, 4.5f, 2f, 1f), element.Bounds);
        Assert.Equal(new Vector2D<float>(5f, 4.5f), element.Position);
    }

    /// <summary>Verifies text, line-limit, and alignment changes reapply the assigned layout.</summary>
    [Fact]
    public void LayoutPropertyChanges_rewrapAndRepositionImmediately()
    {
        var element = CreateTextElement("A");
        var bounds = new Rectangle<float>(10f, 20f, 1f, 2f);
        element.Arrange(bounds);

        element.Text = "AB";
        Assert.Equal(2UL, DrawableTestData.TextInstanceCount(element));
        Assert.Equal(new Rectangle<float>(10f, 20f, 1f, 2f), element.Bounds);
        Assert.Equal(new Vector2D<float>(10f, 20f), element.Position);

        element.MaximumLines = 1;
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(element));
        Assert.Equal(new Vector2D<float>(10f, 20.5f), element.Position);
        Assert.Equal(new Rectangle<float>(10f, 20.5f, 1f, 1f), element.Bounds);

        element.HorizontalAlignment = AlignHorizontal.Right;
        element.VerticalAlignment = AlignVertical.Bottom;
        Assert.Equal(new Vector2D<float>(10f, 21f), element.Position);
        Assert.Equal(new Rectangle<float>(10f, 21f, 1f, 1f), element.Bounds);
    }

    /// <summary>Creates a text element with deterministic in-memory font data.</summary>
    /// <param name="text">The initial text.</param>
    /// <returns>The configured text element.</returns>
    private static TextElement CreateTextElement(string text) => new(text, new TestTextStyle());

    /// <summary>Provides deterministic single-cell glyphs for layout tests.</summary>
    private sealed class TestTextStyle : ITextStyle
    {
        /// <summary>Initializes the test font metrics.</summary>
        /// <param name="lineHeight">The font line height.</param>
        public TestTextStyle(double lineHeight = 1) => FontMetrics = new(1, 1, 0, lineHeight);

        /// <inheritdoc />
        public ITexture Texture { get; } = new Texture("font", 2, 1, [Colors.White, Colors.White]);

        /// <inheritdoc />
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } =
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
            };

        /// <inheritdoc />
        public FontMetrics FontMetrics { get; }

        /// <inheritdoc />
        public MsdfMetadata Msdf { get; } = new(4, 1);

        /// <inheritdoc />
        public IReadOnlyDictionary<
            (int LeftCodepoint, int RightCodepoint),
            double
        > Kerning
        { get; } = new Dictionary<(int, int), double>();

        /// <inheritdoc />
        public Color Color { get; } = Colors.White;

        /// <inheritdoc />
        public double Size { get; } = 1;
    }
}
