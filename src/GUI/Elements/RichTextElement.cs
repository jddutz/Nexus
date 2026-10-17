using System.Globalization;
using System.Text;
using Nexus.Graphics.Text;

namespace Nexus.GUI.Elements;

/// <summary>Displays inline BBCode-formatted text with wrapping and explicit line breaks.</summary>
/// <remarks>
/// Supported tags are <c>[b]</c>, <c>[i]</c>, <c>[color=#RRGGBB]</c>,
/// <c>[color=#RRGGBBAA]</c>, and <c>[br]</c>. Tags must be properly nested.
/// Use <c>[[</c> to display a literal opening bracket.
/// </remarks>
public sealed class RichTextElement : FlowLayout
{
    private readonly ITextStyle _regularStyle;
    private readonly ITextStyle _boldStyle;
    private readonly ITextStyle _italicStyle;
    private readonly ITextStyle _boldItalicStyle;
    private readonly Color _textColor;
    private readonly ulong _renderLayerMask;

    /// <summary>Initializes a rich-text element from its BBCode-like source markup.</summary>
    /// <param name="markup">The source text containing supported BBCode-like tags.</param>
    /// <param name="regularStyle">The style used for unformatted text.</param>
    /// <param name="boldStyle">The style used by <c>[b]</c> text.</param>
    /// <param name="italicStyle">The style used by <c>[i]</c> text.</param>
    /// <param name="boldItalicStyle">The style used by text with both bold and italic formatting.</param>
    /// <param name="textColor">The color used when a run has no <c>[color]</c> tag.</param>
    /// <param name="renderLayerMask">The layer mask applied to all rendered text.</param>
    /// <exception cref="ArgumentNullException">Markup or a font style is null.</exception>
    /// <exception cref="FormatException">The markup contains an unsupported or unbalanced tag.</exception>
    public RichTextElement(
        string markup,
        ITextStyle regularStyle,
        ITextStyle boldStyle,
        ITextStyle italicStyle,
        ITextStyle boldItalicStyle,
        Color? textColor = null,
        ulong renderLayerMask = RenderLayers.All
    )
    {
        ArgumentNullException.ThrowIfNull(markup);
        _regularStyle = regularStyle ?? throw new ArgumentNullException(nameof(regularStyle));
        _boldStyle = boldStyle ?? throw new ArgumentNullException(nameof(boldStyle));
        _italicStyle = italicStyle ?? throw new ArgumentNullException(nameof(italicStyle));
        _boldItalicStyle =
            boldItalicStyle ?? throw new ArgumentNullException(nameof(boldItalicStyle));
        _textColor = textColor ?? Colors.White;
        _renderLayerMask = renderLayerMask;
        Markup = markup;

        Priority = LayoutPriority.Vertical;
        LayoutMode = LayoutSizingMode.Dynamic;
        HorizontalAlignment = AlignHorizontal.Left;
        VerticalAlignment = AlignVertical.Top;

        var lines = ParseMarkup(markup);
        foreach (var line in lines)
        {
            var layout = new FlowLayout
            {
                LayoutMode = LayoutSizingMode.Dynamic,
                Priority = LayoutPriority.Horizontal,
                HorizontalAlignment = AlignHorizontal.Left,
                VerticalAlignment = AlignVertical.Top,
            };

            if (line.Count == 0)
                line.Add(new(" ", default));

            foreach (var run in line)
            {
                var style = run.Style.Bold
                    ? run.Style.Italic ? _boldItalicStyle : _boldStyle
                    : run.Style.Italic ? _italicStyle : _regularStyle;
                foreach (var token in SplitIntoWords(run.Text))
                {
                    layout.Children.Add(new TextElement(token, style)
                    {
                        Color = run.Style.Color ?? _textColor,
                        HorizontalAlignment = AlignHorizontal.Left,
                        VerticalAlignment = AlignVertical.Top,
                        RenderLayerMask = _renderLayerMask,
                    });
                }
            }

            Children.Add(layout);
        }
    }

    /// <summary>Gets the original markup string rendered by this element.</summary>
    public string Markup { get; }

    /// <summary>Splits markup into styled runs, preserving explicit line breaks.</summary>
    /// <param name="markup">The source markup to parse.</param>
    /// <returns>One ordered list of styled runs for each rendered line.</returns>
    /// <exception cref="FormatException">A tag is unsupported, malformed, or unbalanced.</exception>
    private static List<List<RichTextRun>> ParseMarkup(string markup)
    {
        var lines = new List<List<RichTextRun>> { new() };
        var openTags = new Stack<MarkupFrame>();
        var style = default(MarkupStyle);
        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length == 0)
                return;

            var value = text.ToString();
            var line = lines[^1];
            if (line.Count > 0 && line[^1].Style == style)
                line[^1] = line[^1] with { Text = line[^1].Text + value };
            else
                line.Add(new(value, style));
            text.Clear();
        }

        markup = markup.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        for (var index = 0; index < markup.Length;)
        {
            if (markup[index] != '[')
            {
                if (markup[index] == '\n')
                {
                    FlushText();
                    lines.Add([]);
                }
                else
                {
                    text.Append(markup[index]);
                }
                index++;
                continue;
            }

            if (index + 1 < markup.Length && markup[index + 1] == '[')
            {
                text.Append('[');
                index += 2;
                continue;
            }

            var closingBracket = markup.IndexOf(']', index + 1);
            if (closingBracket < 0)
                throw new FormatException($"Unclosed markup tag at character {index}.");

            var tag = markup[(index + 1)..closingBracket].Trim();
            if (tag.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                FlushText();
                lines.Add([]);
                index = closingBracket + 1;
                continue;
            }

            if (tag.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                OpenTag("b", style with { Bold = true });
            }
            else if (tag.Equals("i", StringComparison.OrdinalIgnoreCase))
            {
                OpenTag("i", style with { Italic = true });
            }
            else if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
            {
                var color = ParseColor(tag["color=".Length..], index);
                OpenTag("color", style with { Color = color });
            }
            else if (tag.Equals("/b", StringComparison.OrdinalIgnoreCase))
            {
                CloseTag("b", index);
            }
            else if (tag.Equals("/i", StringComparison.OrdinalIgnoreCase))
            {
                CloseTag("i", index);
            }
            else if (tag.Equals("/color", StringComparison.OrdinalIgnoreCase))
            {
                CloseTag("color", index);
            }
            else
            {
                throw new FormatException($"Unsupported markup tag '[{tag}]' at character {index}.");
            }

            index = closingBracket + 1;
        }

        FlushText();
        if (openTags.Count > 0)
            throw new FormatException($"Markup tag '[{openTags.Peek().Name}]' is not closed.");

        return lines;

        void OpenTag(string name, MarkupStyle nextStyle)
        {
            FlushText();
            openTags.Push(new(name, style));
            style = nextStyle;
        }

        void CloseTag(string name, int position)
        {
            FlushText();
            if (!openTags.TryPop(out var frame) || !frame.Name.Equals(name, StringComparison.Ordinal))
                throw new FormatException($"Unexpected closing tag '[/{name}]' at character {position}.");
            style = frame.PreviousStyle;
        }
    }

    /// <summary>Converts a supported hexadecimal color tag value into a color.</summary>
    /// <param name="value">The tag value, including its leading hash.</param>
    /// <param name="position">The position of the opening tag in the markup.</param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">The value is not #RRGGBB or #RRGGBBAA.</exception>
    private static Color ParseColor(string value, int position)
    {
        if (value.Length is not (7 or 9) || value[0] != '#')
            throw new FormatException($"Invalid color markup at character {position}.");

        Span<byte> channels = stackalloc byte[value.Length == 7 ? 3 : 4];
        for (var channel = 0; channel < channels.Length; channel++)
        {
            if (
                !byte.TryParse(
                    value.AsSpan(1 + channel * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out channels[channel]
                )
            )
                throw new FormatException($"Invalid color markup at character {position}.");
        }

        return channels.Length == 3
            ? new Color(channels[0], channels[1], channels[2])
            : new Color(channels[0], channels[1], channels[2], channels[3]);
    }

    /// <summary>Splits one run into whitespace and word tokens for inline wrapping.</summary>
    /// <param name="text">The run text to split.</param>
    /// <returns>Ordered non-empty tokens that preserve the original text.</returns>
    private static IEnumerable<string> SplitIntoWords(string text)
    {
        for (var start = 0; start < text.Length;)
        {
            var whitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == whitespace)
                end++;
            yield return text[start..end];
            start = end;
        }
    }

    /// <summary>Describes the active formatting state for one run.</summary>
    /// <param name="Bold">Whether bold formatting is active.</param>
    /// <param name="Italic">Whether italic formatting is active.</param>
    /// <param name="Color">An optional explicit run color.</param>
    private readonly record struct MarkupStyle
    {
        /// <summary>Initializes one markup formatting state.</summary>
        /// <param name="bold">Whether bold formatting is active.</param>
        /// <param name="italic">Whether italic formatting is active.</param>
        /// <param name="color">An optional explicit run color.</param>
        public MarkupStyle(bool bold, bool italic, Color? color)
        {
            Bold = bold;
            Italic = italic;
            Color = color;
        }

        /// <summary>Gets whether bold formatting is active.</summary>
        public bool Bold { get; init; }

        /// <summary>Gets whether italic formatting is active.</summary>
        public bool Italic { get; init; }

        /// <summary>Gets the optional explicit run color.</summary>
        public Color? Color { get; init; }
    }

    /// <summary>Describes the formatting state restored when a tag closes.</summary>
    private readonly record struct MarkupFrame
    {
        /// <summary>Initializes a tag frame.</summary>
        /// <param name="name">The tag name.</param>
        /// <param name="previousStyle">The formatting state before the tag opened.</param>
        public MarkupFrame(string name, MarkupStyle previousStyle)
        {
            Name = name;
            PreviousStyle = previousStyle;
        }

        /// <summary>Gets the tag name.</summary>
        public string Name { get; }

        /// <summary>Gets the formatting state before the tag opened.</summary>
        public MarkupStyle PreviousStyle { get; }
    }

    /// <summary>Describes a source-text run and its active formatting state.</summary>
    private readonly record struct RichTextRun
    {
        /// <summary>Initializes one source-text run.</summary>
        /// <param name="text">The source text in the run.</param>
        /// <param name="style">The active formatting state.</param>
        public RichTextRun(string text, MarkupStyle style)
        {
            Text = text;
            Style = style;
        }

        /// <summary>Gets the source text in the run.</summary>
        public string Text { get; init; }

        /// <summary>Gets the active formatting state.</summary>
        public MarkupStyle Style { get; }
    }
}
