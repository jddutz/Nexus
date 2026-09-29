namespace Nexus.GUI.Elements;

using System.Text;
using Nexus.Graphics.Text;

/// <summary>Specifies horizontal placement within an element's bounds.</summary>
public enum AlignHorizontal
{
    /// <summary>Places content against the left edge.</summary>
    Left,

    /// <summary>Centers content horizontally.</summary>
    Center,

    /// <summary>Places content against the right edge.</summary>
    Right,
}

/// <summary>Specifies vertical placement within an element's bounds.</summary>
public enum AlignVertical
{
    /// <summary>Places content against the top edge.</summary>
    Top,

    /// <summary>Centers content vertically.</summary>
    Center,

    /// <summary>Places content against the bottom edge.</summary>
    Bottom,
}

/// <summary>Specifies horizontal alignment of text within a line.</summary>
public enum TextAlignment
{
    /// <summary>Aligns text to the left.</summary>
    Left,

    /// <summary>Centers text.</summary>
    Center,

    /// <summary>Aligns text to the right.</summary>
    Right,

    /// <summary>Expands spacing so text fills the line width.</summary>
    Justify,
}

/// <summary>Measures and arranges styled text within a GUI element.</summary>
public sealed class TextElement : Element
{
    private readonly List<IGameObject> _visibilityAncestors = [];
    private readonly ITextStyle _style;
    private string _text;
    private int? _maximumLines;
    private ulong _renderLayerMask;
    private TextComponent? _textComponent;
    private Rectangle<float>? _layoutBounds;
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;
    private AlignVertical _verticalAlignment = AlignVertical.Center;

    /// <summary>Gets or sets horizontal placement within the element's bounds.</summary>
    public AlignHorizontal HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            SetProperty(ref _horizontalAlignment, value);
        }
    }

    /// <summary>Gets or sets vertical placement within the element's bounds.</summary>
    public AlignVertical VerticalAlignment
    {
        get => _verticalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            SetProperty(ref _verticalAlignment, value);
        }
    }

    /// <summary>Gets or sets the complete source text.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!SetProperty(ref _text, value))
                return;

            if (_textComponent is not null)
                _textComponent.Text = value;
        }
    }

    /// <summary>Gets the style used to measure and render the text.</summary>
    public ITextStyle Style => _style;

    /// <summary>Gets or sets the maximum number of displayed lines, or null to fit the height.</summary>
    public int? MaximumLines
    {
        get => _maximumLines;
        set
        {
            if (value is <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            SetProperty(ref _maximumLines, value);
        }
    }

    /// <summary>Gets or sets the render-layer mask applied to the text component.</summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (!SetProperty(ref _renderLayerMask, value))
                return;

            if (_textComponent is not null)
                _textComponent.RenderLayerMask = value;
        }
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
    )
        : base()
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        if (maximumLines is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumLines));

        _text = text;
        _style = style;
        _maximumLines = maximumLines;
        _renderLayerMask = renderLayerMask;
        UpdateVisualComponent();
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;

        var wrappedText = WrapTextToBounds(
            _text,
            _style,
            constraint.X,
            constraint.Y,
            _maximumLines
        );
        var measuredSize = MeasureWrappedText(_style, wrappedText);
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
        var wrappedText = WrapTextToBounds(
            _text,
            _style,
            bounds.Size.X,
            bounds.Size.Y,
            _maximumLines
        );
        if (_textComponent.Text != wrappedText)
            _textComponent.Text = wrappedText;

        var textBounds = _textComponent.LayoutBounds;
        var textOrigin = new Vector2D<float>(
            bounds.Origin.X + GetHorizontalOffset(bounds.Size.X, textBounds.Size.X),
            bounds.Origin.Y + GetVerticalOffset(bounds.Size.Y, textBounds.Size.Y)
        );
        Bounds = new Rectangle<float>(textOrigin, textBounds.Size);
        Position = new(textOrigin.X - textBounds.Origin.X, textOrigin.Y - textBounds.Origin.Y);
    }

    /// <summary>Creates a fresh text component from the retained text configuration.</summary>
    private void CreateVisualComponent()
    {
        var textComponent = new TextComponent(_style)
        {
            RenderLayerMask = _renderLayerMask,
            Text = _text,
        };
        _textComponent = textComponent;
        AddComponent(textComponent);
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Gets the horizontal offset for the selected alignment.</summary>
    /// <param name="availableSize">The element's available width.</param>
    /// <param name="contentSize">The rendered text width.</param>
    /// <returns>The offset from the element's left edge.</returns>
    private float GetHorizontalOffset(float availableSize, float contentSize) =>
        _horizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => (availableSize - contentSize) / 2f,
            AlignHorizontal.Right => availableSize - contentSize,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the vertical offset for the selected alignment.</summary>
    /// <param name="availableSize">The element's available height.</param>
    /// <param name="contentSize">The rendered text height.</param>
    /// <returns>The offset from the element's top edge.</returns>
    private float GetVerticalOffset(float availableSize, float contentSize) =>
        _verticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => (availableSize - contentSize) / 2f,
            AlignVertical.Bottom => availableSize - contentSize,
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

        for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(ancestor);
        }
    }

    /// <summary>Updates text component ownership when an ancestor's visibility changes.</summary>
    /// <param name="sender">The ancestor that changed.</param>
    /// <param name="eventArgs">The property-change details.</param>
    private void OnAncestorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is null or nameof(IsVisible))
            UpdateVisualComponent();
    }

    /// <inheritdoc />
    protected override void OnHierarchyChanged()
    {
        base.OnHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateVisualComponent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible))
            UpdateVisualComponent();
    }

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
        while (
            wrappedText.Length > 0
            && MeasureWrappedText(style, wrappedText).Y > availableHeight
        )
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
        new TextSpan(style, text).LayoutBounds.Size.X;

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
    private static Vector2D<float> MeasureWrappedText(ITextStyle style, string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2D<float>.Zero;

        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        var lines = text.Split('\n');
        var top = float.PositiveInfinity;
        var bottom = float.NegativeInfinity;
        var width = 0f;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var bounds = new TextSpan(style, lines[lineIndex]).LayoutBounds;
            width = MathF.Max(width, bounds.Size.X);
            top = MathF.Min(top, bounds.Origin.Y + lineIndex * lineHeight);
            bottom = MathF.Max(bottom, bounds.Max.Y + lineIndex * lineHeight);
        }

        return float.IsFinite(top) && float.IsFinite(bottom)
            ? new Vector2D<float>(width, bottom - top)
            : Vector2D<float>.Zero;
    }

    /// <summary>Gets the number of font lines that fit in an available height.</summary>
    /// <param name="style">The font metrics used to determine line height.</param>
    /// <param name="availableHeight">The available height.</param>
    /// <returns>The maximum number of lines fitting the height.</returns>
    private static int GetMaximumLineCount(ITextStyle style, float availableHeight)
    {
        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            lineHeight = (float)style.Size;

        return lineHeight > 0f ? Math.Max(0, (int)MathF.Floor(availableHeight / lineHeight)) : 0;
    }
}
