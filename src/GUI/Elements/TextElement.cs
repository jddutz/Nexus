namespace Nexus.GUI.Elements;

using System.Text;
using Nexus.Graphics.Text;

/// <summary>Measures and arranges styled text within a GUI element.</summary>
public partial class TextElement : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];

    [Observable]
    private string _text = string.Empty;

    [Observable]
    private int? _maximumLines;

    [Observable]
    private ulong _renderLayerMask = ulong.MaxValue;
    private TextComponent? _textComponent;
    private Rectangle<float>? _layoutBounds;

    /// <summary>Gets or sets the font and visual style used by the text element.</summary>
    [Observable(PublicSetter = true)]
    private ITextStyle? _style;

    [Observable]
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;

    [Observable]
    private AlignVertical _verticalAlignment = AlignVertical.Center;

    private void BeforeHorizontalAlignmentChanges(AlignHorizontal value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void BeforeVerticalAlignmentChanges(AlignVertical value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    protected virtual partial void AfterHorizontalAlignmentChanges(AlignHorizontal previousValue) =>
        ReapplyLayout();

    protected virtual partial void AfterVerticalAlignmentChanges(AlignVertical previousValue) =>
        ReapplyLayout();

    protected virtual partial void AfterMaximumLinesChanges(int? previousValue) => ReapplyLayout();

    protected virtual partial void AfterTextChanges(string previousValue)
    {
        if (_textComponent is not null && _layoutBounds is null)
            _textComponent.Text = Text;
        ReapplyLayout();
    }

    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_textComponent is not null)
            _textComponent.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Updates the graphics component when the text style changes.</summary>
    /// <param name="previousValue">The previous text style.</param>
    protected virtual partial void AfterStyleChanges(ITextStyle? previousValue)
    {
        if (_textComponent is not null)
            _textComponent.TextStyle = Style;
        ReapplyLayout();
    }

    /// <summary>Initializes an empty text element.</summary>
    public TextElement()
    {
        UpdateVisualComponent();
    }

    /// <summary>Initializes a text element with source text, style, and optional line limit.</summary>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font and visual style used to measure and render text.</param>
    /// <param name="maximumLines">The maximum displayed line count, or null to fit the height.</param>
    /// <param name="renderLayerMask">The render-layer mask applied to generated text.</param>
    public TextElement(
        string text,
        ITextStyle style,
        int? maximumLines = null,
        ulong renderLayerMask = ulong.MaxValue
    ) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        if (maximumLines is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumLines));

        Text = text;
        Style = style;
        MaximumLines = maximumLines;
        RenderLayerMask = renderLayerMask;
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;
        if (Style is not { } style)
            return Vector2D<float>.Zero;

        var wrappedText = WrapTextToBounds(
            Text,
            style,
            constraint.X,
            constraint.Y,
            MaximumLines
        );
        var measuredSize = MeasureWrappedText(style, wrappedText);
        return new(
            MathF.Min(measuredSize.X, constraint.X),
            MathF.Min(measuredSize.Y, constraint.Y)
        );
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        if (!IsEffectivelyVisible || _textComponent is null)
            return;

        _layoutBounds = bounds;
        base.Arrange(bounds);
        if (Style is not { } style)
        {
            SetPosition(bounds.Origin);
            SetBounds(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            return;
        }

        var wrappedText = WrapTextToBounds(
            Text,
            style,
            bounds.Size.X,
            bounds.Size.Y,
            MaximumLines
        );
        if (_textComponent.Text != wrappedText)
            _textComponent.Text = wrappedText;

        _textComponent.Destination = bounds;
        _textComponent.Alignment = new Vector2D<float>(
            GetHorizontalAlignment(),
            GetVerticalAlignment()
        );
        var textBounds = _textComponent.LayoutBounds;
        SetPosition(textBounds.Origin);
        SetBounds(textBounds);
    }

    /// <summary>Reapplies the last parent-assigned rectangle after layout-affecting state changes.</summary>
    private void ReapplyLayout()
    {
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Creates a fresh text component from the retained text configuration.</summary>
    private void CreateVisualComponent()
    {
        var textComponent = new TextComponent
        {
            TextStyle = Style,
            RenderLayerMask = RenderLayerMask,
            Text = Text,
        };
        _textComponent = textComponent;
        AddComponent(textComponent);
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Gets the normalized horizontal alignment value for the text component.</summary>
    private float GetHorizontalAlignment() =>
        HorizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => 0.5f,
            AlignHorizontal.Right => 1f,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the normalized vertical alignment value for the text component.</summary>
    private float GetVerticalAlignment() =>
        VerticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => 0.5f,
            AlignVertical.Bottom => 1f,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Removes the current text component and releases the element's reference.</summary>
    private void RemoveVisualComponent()
    {
        var textComponent = _textComponent;
        _textComponent = null;
        if (textComponent is not null)
            RemoveComponent(textComponent);
    }

    /// <summary>Synchronizes text component ownership with effective visibility.</summary>
    private void UpdateVisualComponent()
    {
        if (IsEffectivelyVisible)
        {
            if (_textComponent is null)
                CreateVisualComponent();
        }
        else if (_textComponent is not null)
        {
            RemoveVisualComponent();
        }
    }

    /// <summary>Subscribes to visibility changes on the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (ISceneNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not IObservable observable)
                continue;

            observable.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(observable);
        }
    }

    /// <summary>Updates text component ownership when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The name of the changed property.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            UpdateVisualComponent();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateVisualComponent();
    }

    /// <inheritdoc />
    protected override void AfterIsVisibleChanges() => UpdateVisualComponent();

    /// <summary>Wraps source text to a width and maximum number of lines.</summary>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font metrics used for wrapping.</param>
    /// <param name="availableWidth">The maximum line width.</param>
    /// <param name="maximumLines">The maximum number of lines.</param>
    /// <returns>The wrapped text that fits the requested bounds.</returns>
    private static string WrapText(
        string text,
        ITextStyle style,
        float availableWidth,
        int maximumLines
    )
    {
        if (availableWidth <= 0f || maximumLines <= 0)
            return string.Empty;

        var lines = new List<string>();
        var currentLine = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
            if (MeasureTextWidth(style, candidate) <= availableWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (currentLine.Length > 0)
            {
                if (lines.Count + 1 >= maximumLines)
                {
                    lines.Add(FitTextToWidth(style, candidate, availableWidth));
                    return string.Join('\n', lines);
                }

                lines.Add(currentLine);
                currentLine = string.Empty;
            }

            var remainder = word;
            while (MeasureTextWidth(style, remainder) > availableWidth)
            {
                var fittingPrefix = FitTextToWidth(style, remainder, availableWidth);
                if (fittingPrefix.Length == 0)
                    return string.Join('\n', lines);

                lines.Add(fittingPrefix);
                remainder = remainder[fittingPrefix.Length..];
                if (lines.Count >= maximumLines)
                    return string.Join('\n', lines);
            }

            currentLine = remainder;
        }

        if (currentLine.Length > 0 && lines.Count < maximumLines)
            lines.Add(currentLine);

        return string.Join('\n', lines);
    }

    /// <summary>Wraps text and removes trailing lines whose glyph bounds exceed the height.</summary>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font metrics used for measuring and wrapping.</param>
    /// <param name="availableWidth">The maximum line width.</param>
    /// <param name="availableHeight">The maximum visible glyph height.</param>
    /// <param name="maximumLines">An optional explicit line limit.</param>
    /// <returns>The wrapped text whose visible glyph bounds fit the available size.</returns>
    private static string WrapTextToBounds(
        string text,
        ITextStyle style,
        float availableWidth,
        float availableHeight,
        int? maximumLines
    )
    {
        if (availableHeight <= 0f)
            return string.Empty;

        var lineLimit = GetMaximumLineCount(style, availableHeight);
        if (lineLimit < int.MaxValue)
            lineLimit++;
        if (maximumLines.HasValue)
            lineLimit = Math.Min(lineLimit, maximumLines.Value);

        var wrappedText = WrapText(text, style, availableWidth, lineLimit);
        while (wrappedText.Length > 0 && MeasureWrappedText(style, wrappedText).Y > availableHeight)
        {
            var lastLineStart = wrappedText.LastIndexOf('\n');
            wrappedText = lastLineStart < 0 ? string.Empty : wrappedText[..lastLineStart];
        }

        return wrappedText;
    }

    /// <summary>Measures the visible glyph width of a candidate line.</summary>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="text">The candidate line.</param>
    /// <returns>The visible glyph width.</returns>
    private static float MeasureTextWidth(ITextStyle style, string text) =>
        MeasureTextBounds(style, text).Size.X;

    /// <summary>Measures text by preparing it with a temporary graphics component.</summary>
    /// <param name="style">The font and visual style used by the text.</param>
    /// <param name="text">The text to measure.</param>
    /// <returns>The glyph bounds in text-local coordinates.</returns>
    private static Rectangle<float> MeasureTextBounds(ITextStyle style, string text)
    {
        var component = new TextComponent(style) { Text = text };
        var size = component.Measure(
            new Vector2D<float>(float.PositiveInfinity, float.PositiveInfinity)
        );
        return new Rectangle<float>(0f, 0f, size.X, size.Y);
    }

    /// <summary>Returns the longest leading rune sequence that fits within the available width.</summary>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="text">The text to crop.</param>
    /// <param name="availableWidth">The maximum visible width.</param>
    /// <returns>The fitting prefix.</returns>
    private static string FitTextToWidth(ITextStyle style, string text, float availableWidth)
    {
        var prefix = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var candidate = prefix.ToString() + rune;
            if (MeasureTextWidth(style, candidate) > availableWidth)
                break;

            prefix.Append(rune);
        }

        return prefix.ToString();
    }

    /// <summary>Measures combined visible bounds of newline-separated text spans.</summary>
    /// <param name="style">The font metrics used to determine line positions.</param>
    /// <param name="text">The wrapped text.</param>
    /// <returns>The combined glyph bounds size.</returns>
    /// <summary>Measures already wrapped lines using the shared text layout algorithm.</summary>
    /// <param name="style">The style used to measure line extents.</param>
    /// <param name="text">The text containing explicit line breaks.</param>
    /// <returns>The fitted logical size.</returns>
    private static Vector2D<float> MeasureWrappedText(ITextStyle style, string text) =>
        MeasureTextBounds(style, text).Size;

    /// <summary>Gets the number of font lines that fit in an available height.</summary>
    /// <param name="style">The font metrics used to determine line height.</param>
    /// <param name="availableHeight">The available height.</param>
    /// <returns>The maximum number of lines fitting the height.</returns>
    private static int GetMaximumLineCount(ITextStyle style, float availableHeight)
    {
        var metrics = style.FontMetrics;
        if (
            !double.IsFinite(style.Size)
            || style.Size <= 0d
            || !double.IsFinite(metrics.EmSize)
            || metrics.EmSize <= 0d
            || !double.IsFinite(metrics.LineHeight)
            || metrics.LineHeight <= 0d
        )
            throw new InvalidOperationException("Text style font metrics must be finite and positive.");

        var lineHeight = (float)(metrics.LineHeight * (style.Size / metrics.EmSize));
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            throw new InvalidOperationException("Text style line height must be finite and positive.");

        var lineCount = MathF.Floor(availableHeight / lineHeight);
        return lineCount >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)lineCount);
    }
}
