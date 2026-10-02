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
        element.Arrange(new Rectangle<float>(0f, 0f, 2f, 2f));
        var graphics = Assert.Single(element.Components.OfType<IGraphicsComponent>());

        Assert.Equal("A", element.Text);
        Assert.Empty(graphics.Drawables);

        element.Style = new TestTextStyle();

        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(element));
    }

    /// <summary>Verifies alignment affects text without replacing the assigned element bounds.</summary>
    [Fact]
    public void Arrange_keepsElementPlacementIndependentFromTextAlignment()
    {
        var element = CreateTextElement("A");
        var bounds = new Rectangle<float>(10f, 20f, 8f, 6f);

        element.Arrange(bounds);
        var text = Assert.IsType<TextComponent>(Assert.Single(element.Components));
        Assert.Equal(text.LayoutBounds, element.Bounds);
        Assert.Equal(
            new Rectangle<float>(13.5f, 22.5f, 1f, 1f),
            text.LayoutBounds
        );

        element.HorizontalAlignment = AlignHorizontal.Right;
        element.VerticalAlignment = AlignVertical.Bottom;

        Assert.Equal(text.LayoutBounds, element.Bounds);
        Assert.Equal(
            new Rectangle<float>(17f, 25f, 1f, 1f),
            text.LayoutBounds
        );
    }

    /// <summary>Verifies a line is omitted when its complete line box exceeds available height.</summary>
    [Fact]
    public void Arrange_omitsTextWhenLineBoxExceedsAvailableHeight()
    {
        var element = new TextElement("A", new TestTextStyle(lineHeight: 2));
        var measuredSize = element.Measure(new(10f, 1f));

        element.Arrange(new Rectangle<float>(0f, 0f, measuredSize.X, measuredSize.Y));

        Assert.Equal(Vector2D<float>.Zero, measuredSize);
        Assert.Empty(element.Components.OfType<IGraphicsComponent>().Single().Drawables);
        Assert.Equal(Vector2D<float>.Zero, element.Bounds.Size);
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
        Assert.Equal(Vector2D<float>.Zero, element.Bounds.Size);
        Assert.Equal(Vector2D<float>.Zero, element.Measure(new(20f, 20f)));
        element.Text = "BA";

        parent.IsVisible = true;

        var recreated = DrawableTestData.TextGraphics(element);
        Assert.NotNull(recreated);
        Assert.NotSame(original, recreated);
        Assert.Equal(2UL, DrawableTestData.TextInstanceCount(element));
        Assert.Equal(new Vector2D<float>(2f, 1f), element.Measure(new(20f, 20f)));
        var text = Assert.IsType<TextComponent>(Assert.Single(element.Components));
        Assert.Equal(bounds, text.Destination);
        Assert.Equal(text.LayoutBounds, element.Bounds);
    }

    /// <summary>Verifies text, line-limit, and alignment changes reapply the assigned layout.</summary>
    [Fact]
    public void LayoutPropertyChanges_rewrapAndReapplyLayoutImmediately()
    {
        var element = CreateTextElement("A");
        var bounds = new Rectangle<float>(10f, 20f, 1f, 2f);
        element.Arrange(bounds);

        element.Text = "AB";
        Assert.Equal(2UL, DrawableTestData.TextInstanceCount(element));
        var text = Assert.IsType<TextComponent>(Assert.Single(element.Components));
        Assert.Equal(bounds, text.Destination);
        Assert.Equal(text.LayoutBounds, element.Bounds);

        element.MaximumLines = 1;
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(element));
        Assert.Equal(bounds, text.Destination);
        Assert.Equal(text.LayoutBounds, element.Bounds);
        Assert.Equal(1, text.MaximumLines);
        Assert.Equal("AB", text.Text);
        Assert.True(text.Wrap);

        element.HorizontalAlignment = AlignHorizontal.Right;
        element.VerticalAlignment = AlignVertical.Bottom;
        Assert.Equal(text.LayoutBounds, element.Bounds);
    }

    /// <summary>Verifies a hidden element retains newly assigned bounds for restoration.</summary>
    [Fact]
    public void Arrange_stores_bounds_while_hidden()
    {
        var parent = new Element();
        var element = CreateTextElement("A");
        element.Arrange(new Rectangle<float>(1f, 2f, 4f, 3f));
        parent.AddChild(element);
        parent.IsVisible = false;
        var hiddenBounds = new Rectangle<float>(5f, 6f, 8f, 7f);

        element.Arrange(hiddenBounds);
        Assert.Equal(hiddenBounds.Origin, element.Bounds.Origin);
        Assert.Equal(Vector2D<float>.Zero, element.Bounds.Size);
        parent.IsVisible = true;

        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(element));
        var text = Assert.IsType<TextComponent>(Assert.Single(element.Components));
        Assert.Equal(hiddenBounds, text.Destination);
        Assert.Equal(text.LayoutBounds, element.Bounds);
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
