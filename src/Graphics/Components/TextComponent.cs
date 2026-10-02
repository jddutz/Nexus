namespace Nexus.Graphics.Components;

using System.Text;

/// <summary>Layouts styled text and exposes its prepared glyph drawable.</summary>
public partial class TextComponent : Component, IGraphicsComponent
{
    private List<TextSpan> _spans = [];
    private IReadOnlyList<IDrawable> _drawables = Array.Empty<IDrawable>();
    private Rectangle<float> _layoutBounds;

    /// <summary>Initializes an empty text component that can receive its style later.</summary>
    public TextComponent() { }

    /// <summary>Initializes a text component with the style used by its glyphs.</summary>
    /// <param name="style">The font and visual style used by the text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is null.</exception>
    public TextComponent(ITextStyle style)
        : this()
    {
        TextStyle = style ?? throw new ArgumentNullException(nameof(style));
    }

    /// <summary>Gets the current style, or throws when no style is assigned.</summary>
    public ITextStyle Style =>
        TextStyle ?? throw new InvalidOperationException("A text style has not been assigned.");

    /// <summary>Gets or sets the style used to lay out and render the text.</summary>
    [Observable(PublicSetter = true)]
    private ITextStyle? _textStyle;

    /// <summary>Rebuilds glyph output after the text style changes.</summary>
    /// <param name="previousValue">The previous text style.</param>
    protected virtual partial void AfterTextStyleChanges(ITextStyle? previousValue) =>
        RebuildDrawable();

    /// <summary>Gets or sets the source text represented by this component.</summary>
    [Observable(PublicSetter = true)]
    private string _text = string.Empty;

    /// <summary>Rebuilds glyph output after source text changes.</summary>
    /// <param name="previousValue">The previous source text.</param>
    protected virtual partial void AfterTextChanges(string previousValue)
        => RebuildDrawable();

    /// <summary>Gets or sets the destination rectangle used for layout and placement.</summary>
    [Observable(PublicSetter = true)]
    private Rectangle<float> _destination;

    /// <summary>Updates glyph placement after destination changes.</summary>
    /// <param name="previousValue">The previous destination rectangle.</param>
    protected virtual partial void AfterDestinationChanges(Rectangle<float> previousValue)
        => RebuildDrawable();

    /// <summary>Gets or sets normalized horizontal and vertical alignment within the destination.</summary>
    [Observable(PublicSetter = true)]
    private Vector2D<float> _alignment = Vector2D<float>.Zero;

    /// <summary>Updates glyph placement after alignment changes.</summary>
    /// <param name="previousValue">The previous normalized alignment.</param>
    protected virtual partial void AfterAlignmentChanges(Vector2D<float> previousValue)
        => RebuildDrawable();

    /// <summary>Gets or sets the maximum number of laid-out lines, or null for no explicit limit.</summary>
    [Observable(PublicSetter = true)]
    private int? _maximumLines;

    /// <summary>Rebuilds glyph layout after the maximum line count changes.</summary>
    /// <param name="previousValue">The previous maximum line count.</param>
    protected virtual partial void AfterMaximumLinesChanges(int? previousValue)
        => RebuildDrawable();

    /// <summary>Gets or sets whether lines wrap to the destination width.</summary>
    [Observable(PublicSetter = true)]
    private bool _wrap = true;

    /// <summary>Rebuilds glyph layout after wrapping changes.</summary>
    /// <param name="previousValue">The previous wrapping setting.</param>
    protected virtual partial void AfterWrapChanges(bool previousValue) =>
        RebuildDrawable();

    /// <summary>Gets or sets the render-layer mask applied to the glyph drawable.</summary>
    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    /// <summary>Applies the changed render-layer mask to the current drawable.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_spans.Count > 0)
            foreach (var span in _spans)
                span.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Gets the final glyph geometry bounds in destination coordinates.</summary>
    public Rectangle<float> LayoutBounds => _layoutBounds;

    /// <summary>Gets the drawables currently exposed by this component.</summary>
    public IReadOnlyList<IDrawable> Drawables => _drawables;

    /// <summary>Measures fitted text without changing drawable membership or placement.</summary>
    /// <param name="constraint">The available width and height; positive infinity is unconstrained.</param>
    /// <returns>The measured extent of the fitted lines.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A constraint is NaN or negative.</exception>
    public Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateConstraint(constraint.X, nameof(constraint));
        ValidateConstraint(constraint.Y, nameof(constraint));
        if (TextStyle is not { } style)
            throw new InvalidOperationException("A text style has not been assigned.");
        if (Text is null || MaximumLines is <= 0)
            throw new InvalidOperationException("Text component state is invalid.");

        return CreateLayout(
            style,
            Text,
            constraint.X,
            constraint.Y,
            MaximumLines,
            Wrap,
            Vector2D<float>.Zero,
            Vector2D<float>.Zero
        ).MeasuredSize;
    }

    /// <summary>Validates one non-negative finite or positive-infinite constraint dimension.</summary>
    /// <param name="value">The proposed dimension.</param>
    /// <param name="parameterName">The parameter name to report for invalid values.</param>
    private static void ValidateConstraint(float value, string parameterName)
    {
        if (float.IsNaN(value) || value < 0f)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    /// <summary>Determines whether destination coordinates and dimensions are valid.</summary>
    /// <param name="destination">The destination rectangle to validate.</param>
    /// <returns>True when all coordinates are finite and dimensions are non-negative.</returns>
    private static bool IsValidDestination(Rectangle<float> destination) =>
        float.IsFinite(destination.Origin.X)
        && float.IsFinite(destination.Origin.Y)
        && float.IsFinite(destination.Size.X)
        && float.IsFinite(destination.Size.Y)
        && destination.Size.X >= 0f
        && destination.Size.Y >= 0f;

    /// <summary>Determines whether alignment is finite and normalized.</summary>
    /// <param name="alignment">The normalized alignment to validate.</param>
    /// <returns>True when both components are in the inclusive range from zero to one.</returns>
    private static bool IsValidAlignment(Vector2D<float> alignment) =>
        float.IsFinite(alignment.X)
        && float.IsFinite(alignment.Y)
        && alignment.X is >= 0f and <= 1f
        && alignment.Y is >= 0f and <= 1f;

    /// <summary>Rebuilds layout, synchronizes the retained span, and updates drawable membership.</summary>
    private void RebuildDrawable()
    {
        if (
            TextStyle is not { } style
            || Text is null
            || MaximumLines is <= 0
            || !IsValidAlignment(Alignment)
            || !IsValidDestination(Destination)
        )
        {
            UnregisterDrawables();
            _layoutBounds = new Rectangle<float>(0f, 0f, 0f, 0f);
            return;
        }

        var layout = CreateLayout(
            style,
            Text,
            Destination.Size.X,
            Destination.Size.Y,
            MaximumLines,
            Wrap,
            Destination.Origin,
            Alignment
        );
        _layoutBounds =
            layout.Instances.Length == 0
                ? new Rectangle<float>(Destination.Origin, Vector2D<float>.Zero)
                : layout.Bounds;

        if (layout.Instances.Length == 0)
        {
            UnregisterDrawables();
            return;
        }

        if (_spans.Count == 0)
        {
            var span = new TextSpan();
            SynchronizeSpan(span, style);
            span.SetInstances(layout.Instances);
            RegisterDrawable(span);
            return;
        }

        var existing = _spans[0];
        SynchronizeSpan(existing, style);
        existing.SetInstances(layout.Instances);
    }

    /// <summary>Synchronizes all component-owned render configuration onto an existing span.</summary>
    /// <param name="span">The drawable to update.</param>
    /// <param name="style">The style providing texture and glyph metrics.</param>
    private void SynchronizeSpan(TextSpan span, ITextStyle style)
    {
        span.Texture = style.Texture;
        span.GlyphScale = GetScale(style);
        span.DistanceRange = checked((float)style.Msdf.DistanceRange);
        span.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Creates the fitted line and glyph result shared by measuring and arrangement.</summary>
    /// <param name="style">The style supplying glyph and font metrics.</param>
    /// <param name="text">The source text with CRLF, CR, and LF line endings.</param>
    /// <param name="availableWidth">The available width; positive infinity is unconstrained.</param>
    /// <param name="availableHeight">The available height; positive infinity is unconstrained.</param>
    /// <param name="maximumLines">The optional maximum number of lines.</param>
    /// <param name="wrap">Whether to prefer word boundaries for automatic wrapping.</param>
    /// <param name="origin">The destination origin.</param>
    /// <param name="alignment">The normalized horizontal and vertical alignment.</param>
    /// <returns>The fitted lines, glyph instances, visible bounds, and logical size.</returns>
    private static TextLayoutResult CreateLayout(
        ITextStyle style,
        string text,
        float availableWidth,
        float availableHeight,
        int? maximumLines,
        bool wrap,
        Vector2D<float> origin,
        Vector2D<float> alignment
    )
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(text);
        var scale = GetScale(style);
        var lineHeight = GetLineHeight(style, scale);
        if (text.Length == 0 || maximumLines == 0 || availableWidth == 0f || availableHeight == 0f)
            return TextLayoutResult.Empty;

        var sourceLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None);
        var lines = new List<LayoutLine>();
        foreach (var sourceLine in sourceLines)
        {
            var runes = sourceLine.EnumerateRunes().ToArray();
            if (!AppendFittedLines(lines, runes, style, availableWidth, wrap, maximumLines))
                break;
            if (maximumLines is { } lineLimit && lines.Count >= lineLimit)
                break;
        }

        var heightLineCount =
            float.IsPositiveInfinity(availableHeight)
            || availableHeight >= lines.Count * lineHeight
                ? lines.Count
                : Math.Max(0, (int)MathF.Floor(availableHeight / lineHeight));
        if (maximumLines is { } maximumLineCount)
            heightLineCount = Math.Min(heightLineCount, maximumLineCount);
        if (heightLineCount == 0)
            return TextLayoutResult.Empty;

        lines.RemoveRange(heightLineCount, lines.Count - heightLineCount);
        var blockHeight = lines.Count * lineHeight;
        var verticalSlack = float.IsFinite(availableHeight)
            ? (availableHeight - blockHeight) * alignment.Y
            : 0f;
        var prepared = new List<GlyphInstance>();
        var measuredWidth = 0f;

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            measuredWidth = MathF.Max(measuredWidth, line.Width);
            var horizontalSlack = float.IsFinite(availableWidth)
                ? (availableWidth - line.Width) * alignment.X
                : 0f;
            var lineOffset = origin.X + horizontalSlack - line.Left;
            foreach (var instance in CreateLineInstances(style, line.Runes, lineIndex * lineHeight))
            {
                prepared.Add(
                    new GlyphInstance(
                        instance.Glyph,
                        new Vector2D<float>(
                            instance.Position.X + lineOffset,
                            instance.Position.Y
                        ),
                        instance.Color
                    )
                );
            }
        }

        var unshiftedBounds = CalculateGlyphBounds(style, prepared);
        var verticalShift = origin.Y + verticalSlack - unshiftedBounds.Origin.Y;
        for (var index = 0; index < prepared.Count; index++)
        {
            var instance = prepared[index];
            prepared[index] = new GlyphInstance(
                instance.Glyph,
                new Vector2D<float>(instance.Position.X, instance.Position.Y + verticalShift),
                instance.Color
            );
        }

        var instances = prepared.ToArray();
        return new TextLayoutResult(
            lines,
            instances,
            CalculateGlyphBounds(style, instances),
            new Vector2D<float>(measuredWidth, blockHeight)
        );
    }

    /// <summary>Adds the width-fitted segments of one explicit line.</summary>
    /// <param name="lines">The collection of prepared output lines.</param>
    /// <param name="runes">The source runes in the explicit line.</param>
    /// <param name="style">The style used for measuring extents.</param>
    /// <param name="availableWidth">The maximum line extent.</param>
    /// <param name="wrap">Whether word boundaries are preferred.</param>
    /// <param name="maximumLines">The optional output line limit.</param>
    /// <returns>True when the explicit line was entirely consumed or fitted.</returns>
    private static bool AppendFittedLines(
        List<LayoutLine> lines,
        Rune[] runes,
        ITextStyle style,
        float availableWidth,
        bool wrap,
        int? maximumLines
    )
    {
        if (lines.Count == maximumLines)
            return false;
        if (runes.Length == 0)
        {
            lines.Add(CreateLayoutLine(style, []));
            return true;
        }

        var start = 0;
        while (start < runes.Length)
        {
            if (lines.Count == maximumLines)
                return false;

            var fittingCount = FindFittingPrefixLength(style, runes, start, availableWidth);
            if (fittingCount == runes.Length - start)
            {
                lines.Add(CreateLayoutLine(style, runes[start..]));
                return true;
            }

            if (fittingCount == 0)
            {
                if (wrap)
                    return false;

                lines.Add(CreateLayoutLine(style, []));
                return true;
            }

            if (!wrap)
            {
                lines.Add(CreateLayoutLine(style, runes[start..(start + fittingCount)]));
                return true;
            }

            var breakIndex = FindWordBreak(runes, start, fittingCount);
            var lineEnd = breakIndex >= 0 ? breakIndex : start + fittingCount;
            lines.Add(CreateLayoutLine(style, runes[start..lineEnd]));
            start = breakIndex >= 0 ? breakIndex + 1 : lineEnd;
            while (start < runes.Length && IsWrapWhitespace(runes[start]))
                start++;
        }

        return true;
    }

    /// <summary>Finds the longest rune prefix whose combined advances and glyphs fit.</summary>
    /// <param name="style">The style used to calculate each candidate line extent.</param>
    /// <param name="runes">The complete source rune array.</param>
    /// <param name="start">The first rune to consider.</param>
    /// <param name="availableWidth">The available width.</param>
    /// <returns>The number of consecutive runes that fit.</returns>
    private static int FindFittingPrefixLength(
        ITextStyle style,
        Rune[] runes,
        int start,
        float availableWidth
    )
    {
        if (float.IsPositiveInfinity(availableWidth))
            return runes.Length - start;

        var fittingCount = 0;
        for (var end = start + 1; end <= runes.Length; end++)
        {
            if (CreateLayoutLine(style, runes[start..end]).Width > availableWidth)
                break;
            fittingCount++;
        }

        return fittingCount;
    }

    /// <summary>Finds the latest ordinary whitespace break inside a fitting prefix.</summary>
    /// <param name="runes">The source rune array.</param>
    /// <param name="start">The start of the line segment.</param>
    /// <param name="fittingCount">The number of runes that fit.</param>
    /// <returns>The boundary index, or -1 if the line must split within a word.</returns>
    private static int FindWordBreak(Rune[] runes, int start, int fittingCount)
    {
        var lastCandidate = Math.Min(start + fittingCount, runes.Length - 1);
        for (var index = lastCandidate; index > start; index--)
        {
            if (IsWrapWhitespace(runes[index]))
                return index;
        }

        return -1;
    }

    /// <summary>Determines whether a rune may be consumed as a wrapping separator.</summary>
    /// <param name="rune">The rune to inspect.</param>
    /// <returns>True for breakable whitespace, excluding nonbreaking space.</returns>
    private static bool IsWrapWhitespace(Rune rune) =>
        Rune.IsWhiteSpace(rune) && rune.Value != 0xA0;

    /// <summary>Calculates a line's advance and visible geometry extent.</summary>
    /// <param name="style">The style supplying glyphs, advances, and kerning.</param>
    /// <param name="runes">The line's source runes.</param>
    /// <returns>The source runes and the complete horizontal extent.</returns>
    private static LayoutLine CreateLayoutLine(ITextStyle style, Rune[] runes)
    {
        var scale = GetScale(style);
        var penX = 0f;
        var left = 0f;
        var right = 0f;
        var previousCodepoint = (int?)null;
        foreach (var rune in runes)
        {
            if (!style.Glyphs.TryGetValue(rune.Value, out var glyph))
            {
                previousCodepoint = null;
                continue;
            }

            if (
                previousCodepoint is { } previous
                && style.Kerning.TryGetValue((previous, glyph.Codepoint), out var kerning)
            )
                penX += checked((float)kerning) * scale;

            var planeBounds = glyph.PlaneBounds;
            if (planeBounds.Right > planeBounds.Left && planeBounds.Top > planeBounds.Bottom)
            {
                left = MathF.Min(left, penX + checked((float)planeBounds.Left) * scale);
                right = MathF.Max(right, penX + checked((float)planeBounds.Right) * scale);
            }

            penX += checked((float)glyph.Advance) * scale;
            right = MathF.Max(right, penX);
            previousCodepoint = glyph.Codepoint;
        }

        return new LayoutLine(runes, left, right);
    }

    /// <summary>Creates baseline-positioned glyph instances for one retained line.</summary>
    /// <param name="style">The style supplying glyphs and kerning.</param>
    /// <param name="runes">The source runes to prepare.</param>
    /// <param name="baselineY">The line's baseline position.</param>
    /// <returns>The glyph instances in unaligned local coordinates.</returns>
    private static IEnumerable<GlyphInstance> CreateLineInstances(
        ITextStyle style,
        Rune[] runes,
        float baselineY
    )
    {
        var scale = GetScale(style);
        var penX = 0f;
        var previousCodepoint = (int?)null;
        foreach (var rune in runes)
        {
            if (!style.Glyphs.TryGetValue(rune.Value, out var glyph))
            {
                previousCodepoint = null;
                continue;
            }

            if (
                previousCodepoint is { } previous
                && style.Kerning.TryGetValue((previous, glyph.Codepoint), out var kerning)
            )
                penX += checked((float)kerning) * scale;

            yield return new GlyphInstance(glyph, new Vector2D<float>(penX, baselineY), style.Color);
            penX += checked((float)glyph.Advance) * scale;
            previousCodepoint = glyph.Codepoint;
        }
    }

    /// <summary>Stores one line's source runes and horizontal extent.</summary>
    private sealed class LayoutLine
    {
        /// <summary>Initializes one line descriptor.</summary>
        /// <param name="runes">The retained source runes.</param>
        /// <param name="left">The left edge, including negative bearings.</param>
        /// <param name="right">The right edge, including advances.</param>
        public LayoutLine(Rune[] runes, float left, float right)
        {
            Runes = runes;
            Left = left;
            Right = right;
        }

        /// <summary>Gets the retained runes.</summary>
        public Rune[] Runes { get; }

        /// <summary>Gets the left edge of this line's full extent.</summary>
        public float Left { get; }

        /// <summary>Gets the right edge of this line's full extent.</summary>
        public float Right { get; }

        /// <summary>Gets the width of this line's full extent.</summary>
        public float Width => Right - Left;
    }

    /// <summary>Stores the fitted lines, prepared instances, bounds, and measured extent.</summary>
    private sealed class TextLayoutResult
    {
        /// <summary>Initializes a completed text layout.</summary>
        /// <param name="lines">The lines retained by layout fitting.</param>
        /// <param name="instances">The prepared glyph instances.</param>
        /// <param name="bounds">The visible glyph geometry bounds.</param>
        /// <param name="measuredSize">The fitted logical extent.</param>
        public TextLayoutResult(
            IReadOnlyList<LayoutLine> lines,
            GlyphInstance[] instances,
            Rectangle<float> bounds,
            Vector2D<float> measuredSize
        )
        {
            Lines = lines;
            Instances = instances;
            Bounds = bounds;
            MeasuredSize = measuredSize;
        }

        /// <summary>Gets the lines retained by fitting.</summary>
        public IReadOnlyList<LayoutLine> Lines { get; }

        /// <summary>Gets the prepared glyph instances.</summary>
        public GlyphInstance[] Instances { get; }

        /// <summary>Gets the visible glyph geometry bounds.</summary>
        public Rectangle<float> Bounds { get; }

        /// <summary>Gets the logical width and height of the fitted output.</summary>
        public Vector2D<float> MeasuredSize { get; }

        /// <summary>Gets the empty layout result.</summary>
        public static TextLayoutResult Empty { get; } = new(
            Array.Empty<LayoutLine>(),
            [],
            new Rectangle<float>(0f, 0f, 0f, 0f),
            Vector2D<float>.Zero
        );
    }

    /// <summary>Calculates the union of nonzero-area glyph geometry.</summary>
    /// <param name="style">The style used to scale font-unit bounds.</param>
    /// <param name="instances">The prepared baseline positions and glyphs.</param>
    /// <returns>The combined glyph bounds in the instance coordinate space.</returns>
    private static Rectangle<float> CalculateGlyphBounds(
        ITextStyle style,
        IReadOnlyList<GlyphInstance> instances
    )
    {
        var scale = GetScale(style);
        var left = float.PositiveInfinity;
        var top = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        var bottom = float.NegativeInfinity;
        foreach (var instance in instances)
        {
            var planeBounds = instance.Glyph.PlaneBounds;
            if (planeBounds.Right <= planeBounds.Left || planeBounds.Top <= planeBounds.Bottom)
                continue;

            left = MathF.Min(left, instance.Position.X + checked((float)planeBounds.Left) * scale);
            top = MathF.Min(top, instance.Position.Y - checked((float)planeBounds.Top) * scale);
            right = MathF.Max(right, instance.Position.X + checked((float)planeBounds.Right) * scale);
            bottom = MathF.Max(bottom, instance.Position.Y - checked((float)planeBounds.Bottom) * scale);
        }

        return float.IsFinite(left)
            ? new Rectangle<float>(left, top, right - left, bottom - top)
            : new Rectangle<float>(0f, 0f, 0f, 0f);
    }

    /// <summary>Gets the validated scale from font units to requested text units.</summary>
    /// <param name="style">The style supplying the size and font metrics.</param>
    /// <returns>The finite positive font-unit scale.</returns>
    /// <exception cref="InvalidOperationException">Font size or em size is invalid.</exception>
    private static float GetScale(ITextStyle style)
    {
        if (
            !double.IsFinite(style.Size)
            || style.Size <= 0d
            || !double.IsFinite(style.FontMetrics.EmSize)
            || style.FontMetrics.EmSize <= 0d
        )
            throw new InvalidOperationException("Text style size and em size must be finite and positive.");

        var scale = (float)(style.Size / style.FontMetrics.EmSize);
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new InvalidOperationException("Text style metrics produce an invalid glyph scale.");

        return scale;
    }

    /// <summary>Gets the validated distance between text baselines.</summary>
    /// <param name="style">The style supplying line metrics and size.</param>
    /// <param name="scale">The font-unit scale.</param>
    /// <returns>The finite positive distance between baselines.</returns>
    /// <exception cref="InvalidOperationException">The font line height is invalid.</exception>
    private static float GetLineHeight(ITextStyle style, float scale)
    {
        var lineHeight = (float)style.FontMetrics.LineHeight * scale;
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            throw new InvalidOperationException("Text style line height must be finite and positive.");

        return lineHeight;
    }

    /// <summary>Registers a new glyph span with this component.</summary>
    /// <param name="span">The span to expose.</param>
    private void RegisterDrawable(TextSpan span)
    {
        _spans.Add(span);
        _drawables = _spans.Cast<IDrawable>().ToArray();
        DrawableAdded?.Invoke(this, new DrawableEventArgs(span));
    }

    /// <summary>Unregisters every glyph span currently exposed by this component.</summary>
    private void UnregisterDrawables()
    {
        if (_spans.Count == 0)
            return;

        var spans = _spans.ToArray();
        _spans.Clear();
        _drawables = Array.Empty<IDrawable>();
        foreach (var span in spans)
            DrawableRemoved?.Invoke(this, new DrawableEventArgs(span));
    }

    /// <summary>Occurs when a drawable is added to this component.</summary>
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <summary>Occurs when a drawable is removed from this component.</summary>
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;
}
