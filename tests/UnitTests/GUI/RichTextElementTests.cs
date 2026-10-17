using Nexus.Assets.Fonts;
using Nexus.Graphics;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI.Elements;

namespace Tests;

/// <summary>Tests BBCode parsing, formatting, and line creation for rich text.</summary>
public sealed class RichTextElementTests
{
    /// <summary>Verifies nested styles and hexadecimal colors become inline text runs.</summary>
    [Fact]
    public void Constructor_appliesNestedStylesAndColors()
    {
        var regular = new TestTextStyle();
        var bold = new TestTextStyle();
        var italic = new TestTextStyle();
        var boldItalic = new TestTextStyle();
        var element = CreateElement(
            "A [b]B[/b] [i]C[/i] [b][i]D[/i][/b][color=#FF000080]E[/color]",
            regular,
            bold,
            italic,
            boldItalic,
            new Color(1, 2, 3)
        );
        var runs = GetTextRuns(element);

        Assert.Equal(["A", " ", "B", " ", "C", " ", "D", "E"], runs.Select(run => run.Text));
        Assert.Same(regular, runs[0].Style);
        Assert.Same(bold, runs[2].Style);
        Assert.Same(italic, runs[4].Style);
        Assert.Same(boldItalic, runs[6].Style);
        Assert.Equal(new Color(255, 0, 0, 128), runs[7].Color);
        Assert.Equal(new Color(1, 2, 3), runs[0].Color);
    }

    /// <summary>Verifies BBCode and literal newlines create separate wrapping rows.</summary>
    [Fact]
    public void Constructor_createsRowsForExplicitLineBreaks()
    {
        var element = CreateElement("A[br]B\nC");

        Assert.Equal(3, element.Children.Count);
        Assert.Equal(["A", "B", "C"], GetTextRuns(element).Select(run => run.Text));
    }

    /// <summary>Verifies malformed, unknown, and unbalanced markup is rejected explicitly.</summary>
    [Fact]
    public void Constructor_throwsForMalformedMarkup()
    {
        Assert.Throws<FormatException>(() => CreateElement("[b]A[/i]"));
        Assert.Throws<FormatException>(() => CreateElement("[unknown]A[/unknown]"));
        Assert.Throws<FormatException>(() => CreateElement("[color=#GG0000]A[/color]"));
        Assert.Throws<FormatException>(() => CreateElement("[b]A"));
    }

    /// <summary>Creates an element with supplied styles for the formatting test.</summary>
    /// <param name="markup">The rich-text source.</param>
    /// <param name="regular">The normal font style.</param>
    /// <param name="bold">The bold font style.</param>
    /// <param name="italic">The italic font style.</param>
    /// <param name="boldItalic">The combined bold and italic font style.</param>
    /// <param name="textColor">The default glyph color.</param>
    /// <returns>The constructed rich-text element.</returns>
    private static RichTextElement CreateElement(
        string markup,
        ITextStyle? regular = null,
        ITextStyle? bold = null,
        ITextStyle? italic = null,
        ITextStyle? boldItalic = null,
        Color? textColor = null
    )
    {
        regular ??= new TestTextStyle();
        bold ??= new TestTextStyle();
        italic ??= new TestTextStyle();
        boldItalic ??= new TestTextStyle();
        return new RichTextElement(markup, regular, bold, italic, boldItalic, textColor);
    }

    /// <summary>Gets all rendered text elements in row and token order.</summary>
    /// <param name="element">The rich-text element to inspect.</param>
    /// <returns>The descendant text elements.</returns>
    private static TextElement[] GetTextRuns(RichTextElement element) =>
        element.Children
            .OfType<FlowLayout>()
            .SelectMany(line => line.Children.OfType<TextElement>())
            .ToArray();

    /// <summary>Provides in-memory glyph data for rich-text construction tests.</summary>
    private sealed class TestTextStyle : ITextStyle
    {
        /// <summary>Initializes deterministic glyph and font metrics.</summary>
        public TestTextStyle()
        {
            FontMetrics = new(1, 1, 0, 1);
            Glyphs = " ABCDE".ToDictionary(
                character => (int)character,
                character => new FontGlyph(character, 1, new(0, 0, 1, 1), new(0, 0, 1, 1))
            );
        }

        /// <inheritdoc />
        public TextStyleId Id { get; } = new(1);

        /// <inheritdoc />
        public double Size { get; } = 1;

        /// <inheritdoc />
        public ITexture Texture { get; } = new Texture("rich-text-test-font", 2, 1, [Colors.White, Colors.White]);

        /// <inheritdoc />
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

        /// <inheritdoc />
        public FontMetrics FontMetrics { get; }

        /// <inheritdoc />
        public MsdfMetadata Msdf { get; } = new(4, 1);

        /// <inheritdoc />
        public IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double> Kerning { get; } =
            new Dictionary<(int, int), double>();
    }
}
