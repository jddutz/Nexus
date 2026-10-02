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

    /// <summary>Gets the constructor-supplied style, or throws when no style is assigned.</summary>
    public ITextStyle Style =>
        TextStyle ?? throw new InvalidOperationException("A text style has not been assigned.");

    /// <summary>Gets or sets the style used to lay out and render the text.</summary>
    [Observable(PublicSetter = true)]
    private ITextStyle? _textStyle;

    /// <summary>Rebuilds glyph output after the text style changes.</summary>
    /// <param name="previousValue">The previous text style.</param>
    protected virtual partial void AfterTextStyleChanges(ITextStyle? previousValue) =>
        RebuildDrawable(replace: true);

    /// <summary>Gets or sets the source text represented by this component.</summary>
    [Observable(PublicSetter = true)]
    private string _text = string.Empty;

    /// <summary>Replaces the prepared drawable after source text changes.</summary>
    /// <param name="previousValue">The previous source text.</param>
    protected virtual partial void AfterTextChanges(string previousValue)
        => RebuildDrawable(replace: true);

    /// <summary>Gets or sets the destination rectangle used for layout and placement.</summary>
    [Observable(PublicSetter = true)]
    private Rectangle<float> _destination;

    /// <summary>Updates glyph placement after destination changes.</summary>
    /// <param name="previousValue">The previous destination rectangle.</param>
    protected virtual partial void AfterDestinationChanges(Rectangle<float> previousValue)
        => RebuildDrawable(replace: false);

    /// <summary>Gets or sets normalized horizontal and vertical alignment within the destination.</summary>
    [Observable(PublicSetter = true)]
    private Vector2D<float> _alignment = Vector2D<float>.Zero;

    /// <summary>Updates glyph placement after alignment changes.</summary>
    /// <param name="previousValue">The previous normalized alignment.</param>
    protected virtual partial void AfterAlignmentChanges(Vector2D<float> previousValue)
        => RebuildDrawable(replace: false);

    /// <summary>Gets or sets the maximum number of laid-out lines, or null for no explicit limit.</summary>
    [Observable(PublicSetter = true)]
    private int? _maximumLines;

    /// <summary>Rebuilds glyph layout after the maximum line count changes.</summary>
    /// <param name="previousValue">The previous maximum line count.</param>
    protected virtual partial void AfterMaximumLinesChanges(int? previousValue)
        => RebuildDrawable(replace: false);

    /// <summary>Gets or sets whether lines wrap to the destination width.</summary>
    [Observable(PublicSetter = true)]
    private bool _wrap = true;

    /// <summary>Rebuilds glyph layout after wrapping changes.</summary>
    /// <param name="previousValue">The previous wrapping setting.</param>
    protected virtual partial void AfterWrapChanges(bool previousValue) =>
        RebuildDrawable(replace: false);

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

    /// <summary>Measures text without changing the component's drawable or raising drawable events.</summary>
    /// <param name="constraint">The available width and height; positive infinity is unconstrained.</param>
    /// <returns>The measured glyph bounds size, limited by the supplied constraint.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A constraint is NaN or negative.</exception>
    public Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateConstraint(constraint.X, nameof(constraint));
        ValidateConstraint(constraint.Y, nameof(constraint));
        if (TextStyle is not { } style)
            throw new InvalidOperationException("A text style has not been assigned.");
        if (!IsValidState())
            throw new InvalidOperationException("Text component state is invalid.");

        var bounds = MeasureTextBounds(style, Text, constraint.X, MaximumLines, Wrap);
        return new(
            MathF.Min(bounds.Size.X, constraint.X),
            MathF.Min(bounds.Size.Y, constraint.Y)
        );
    }

    /// <summary>Measures glyph geometry with the requested width, line limit, and wrapping.</summary>
    /// <param name="style">The font and visual style used for measurement.</param>
    /// <param name="text">The source text to measure.</param>
    /// <param name="availableWidth">The available width or positive infinity.</param>
    /// <param name="maximumLines">The optional maximum line count.</param>
    /// <param name="wrap">Whether to wrap lines to the available width.</param>
    /// <returns>The combined glyph bounds in text-local coordinates.</returns>
    private static Rectangle<float> MeasureTextBounds(
        ITextStyle style,
        string text,
        float availableWidth,
        int? maximumLines,
        bool wrap
    )
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(text);
        var instances = CreateInstances(style, text, availableWidth, maximumLines, wrap);
        return CalculateBounds(style, instances);
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

    /// <summary>Rebuilds glyph instances and synchronizes drawable membership.</summary>
    /// <param name="replace">Whether to replace an existing span drawable.</param>
    private void RebuildDrawable(bool replace)
    {
        var hadDrawables = _spans.Count > 0;
        if (!IsValidState())
        {
            UnregisterDrawables();
            _layoutBounds = new Rectangle<float>(0f, 0f, 0f, 0f);
            return;
        }

        var style = TextStyle!;
        var availableWidth =
            Destination.Size.X == 0f ? float.PositiveInfinity : Destination.Size.X;
        var instances = CreateInstances(style, Text, availableWidth, MaximumLines, Wrap);
        var localBounds = CalculateBounds(style, instances);
        var offset = new Vector2D<float>(
            Destination.Origin.X
                + (Destination.Size.X - localBounds.Size.X) * Alignment.X
                - localBounds.Origin.X,
            Destination.Origin.Y
                + (Destination.Size.Y - localBounds.Size.Y) * Alignment.Y
                - localBounds.Origin.Y
        );

        for (var index = 0; index < instances.Length; index++)
        {
            var instance = instances[index];
            instances[index] = (
                instance.Glyph,
                new Vector2D<float>(
                    instance.Position.X + offset.X,
                    instance.Position.Y + offset.Y
                ),
                instance.Color
            );
        }

        _layoutBounds =
            instances.Length == 0
                ? new Rectangle<float>(Destination.Origin, Vector2D<float>.Zero)
                : CalculateBounds(style, instances);

        if (replace && hadDrawables)
            UnregisterDrawables();

        if (instances.Length == 0 && _spans.Count == 0 && !hadDrawables && Text.Length == 0)
            return;

        if (_spans.Count == 0)
        {
            RegisterDrawable(
                new TextSpan(style, instances) { RenderLayerMask = RenderLayerMask }
            );
            return;
        }

        _spans[0].UpdateInstances(instances);
    }

    /// <summary>Determines whether the component's current layout inputs are valid.</summary>
    /// <returns>True when all layout values satisfy the text component contract.</returns>
    private bool IsValidState() =>
        TextStyle is not null
        && Text is not null
        && MaximumLines is not <= 0
        && IsValidAlignment(Alignment)
        && IsValidDestination(Destination);

    /// <summary>Creates baseline-positioned glyph instances for wrapped text lines.</summary>
    /// <param name="style">The font and visual style used by the glyphs.</param>
    /// <param name="text">The source text.</param>
    /// <param name="availableWidth">The available width or positive infinity.</param>
    /// <param name="maximumLines">The optional maximum line count.</param>
    /// <param name="wrap">Whether lines wrap to the available width.</param>
    /// <returns>The prepared glyph instances in text-local coordinates.</returns>
    private static (FontGlyph Glyph, Vector2D<float> Position, Color Color)[] CreateInstances(
        ITextStyle style,
        string text,
        float availableWidth,
        int? maximumLines,
        bool wrap
    )
    {
        if (maximumLines == 0 || (wrap && availableWidth == 0f))
            return [];

        var lines = new List<List<Rune>> { new() };
        var currentLine = lines[0];
        var characterOffset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is '\r' or '\n')
            {
                if (maximumLines is { } limit && lines.Count >= limit)
                    break;
                if (
                    rune.Value == '\r'
                    && characterOffset + 1 < text.Length
                    && text[characterOffset + 1] == '\n'
                )
                {
                    characterOffset++;
                }

                currentLine = [];
                lines.Add(currentLine);
                characterOffset += rune.Utf16SequenceLength;
                continue;
            }

            if (maximumLines is { } lineLimit && lines.Count >= lineLimit)
                break;

            if (
                wrap
                && float.IsFinite(availableWidth)
                && currentLine.Count > 0
                && MeasureLineWidth(style, currentLine.Append(rune)) > availableWidth
            )
            {
                currentLine = [];
                lines.Add(currentLine);
                if (maximumLines is { } maximumLineLimit && lines.Count > maximumLineLimit)
                    break;
            }

            currentLine.Add(rune);
            characterOffset += rune.Utf16SequenceLength;
        }

        var scale = GetScale(style);
        var lineHeight = GetLineHeight(style, scale);
        var instances = new List<(FontGlyph Glyph, Vector2D<float> Position, Color Color)>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var penX = 0f;
            var previousCodepoint = (int?)null;
            foreach (var rune in lines[lineIndex])
            {
                if (!style.Glyphs.TryGetValue(rune.Value, out var glyph))
                    continue;

                if (
                    previousCodepoint is { } previous
                    && style.Kerning.TryGetValue((previous, glyph.Codepoint), out var kerning)
                )
                    penX += (float)kerning * scale;

                instances.Add(
                    (
                        glyph,
                        new Vector2D<float>(penX, lineIndex * lineHeight),
                        style.Color
                    )
                );

                penX += (float)glyph.Advance * scale;
                previousCodepoint = glyph.Codepoint;
            }
        }

        return instances.ToArray();
    }

    /// <summary>Measures the visible width of a line, including bearings and advances.</summary>
    /// <param name="style">The font and visual style used by the glyphs.</param>
    /// <param name="runes">The line's Unicode scalar values.</param>
    /// <returns>The visible horizontal extent.</returns>
    private static float MeasureLineWidth(ITextStyle style, IEnumerable<Rune> runes)
    {
        var scale = GetScale(style);
        var penX = 0f;
        var left = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        var previousCodepoint = (int?)null;
        foreach (var rune in runes)
        {
            if (!style.Glyphs.TryGetValue(rune.Value, out var glyph))
                continue;

            if (
                previousCodepoint is { } previous
                && style.Kerning.TryGetValue((previous, glyph.Codepoint), out var kerning)
            )
                penX += (float)kerning * scale;

            left = MathF.Min(left, penX + (float)glyph.PlaneBounds.Left * scale);
            right = MathF.Max(right, penX + (float)glyph.PlaneBounds.Right * scale);
            penX += (float)glyph.Advance * scale;
            right = MathF.Max(right, penX);
            previousCodepoint = glyph.Codepoint;
        }

        return float.IsFinite(left) ? MathF.Max(right, penX) - MathF.Min(left, 0f) : penX;
    }

    /// <summary>Calculates the union of prepared glyph geometry.</summary>
    /// <param name="style">The style used to scale font-unit bounds.</param>
    /// <param name="instances">The prepared baseline positions and glyphs.</param>
    /// <returns>The combined glyph bounds in the instance coordinate space.</returns>
    private static Rectangle<float> CalculateBounds(
        ITextStyle style,
        IReadOnlyList<(FontGlyph Glyph, Vector2D<float> Position, Color Color)> instances
    )
    {
        if (instances.Count == 0)
            return new Rectangle<float>(0f, 0f, 0f, 0f);

        var scale = GetScale(style);
        var left = float.PositiveInfinity;
        var top = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        var bottom = float.NegativeInfinity;
        foreach (var instance in instances)
        {
            var planeBounds = instance.Glyph.PlaneBounds;
            left = MathF.Min(left, instance.Position.X + (float)planeBounds.Left * scale);
            top = MathF.Min(top, instance.Position.Y - (float)planeBounds.Top * scale);
            right = MathF.Max(right, instance.Position.X + (float)planeBounds.Right * scale);
            bottom = MathF.Max(bottom, instance.Position.Y - (float)planeBounds.Bottom * scale);
        }

        return new Rectangle<float>(left, top, right - left, bottom - top);
    }

    /// <summary>Gets the scale from font units to requested text units.</summary>
    /// <param name="style">The style supplying the size and font metrics.</param>
    /// <returns>The finite font-unit scale.</returns>
    private static float GetScale(ITextStyle style) =>
        style.FontMetrics.EmSize == 0 ? 1f : (float)(style.Size / style.FontMetrics.EmSize);

    /// <summary>Gets a positive line height, falling back to the requested text size.</summary>
    /// <param name="style">The style supplying line metrics and size.</param>
    /// <param name="scale">The font-unit scale.</param>
    /// <returns>The distance between line baselines.</returns>
    private static float GetLineHeight(ITextStyle style, float scale)
    {
        var lineHeight = (float)style.FontMetrics.LineHeight * scale;
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            lineHeight = (float)style.Size;
        return float.IsFinite(lineHeight) && lineHeight > 0f ? lineHeight : 1f;
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
