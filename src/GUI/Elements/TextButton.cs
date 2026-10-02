namespace Nexus.GUI.Elements;

using System.Text;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;

/// <summary>
/// Specifies the horizontal alignment of a text button's label.
/// </summary>
public enum TextButtonLabelAlignment
{
    /// <summary>Aligns the label to the leading edge of the padded content area.</summary>
    Start,

    /// <summary>Centers the label within the button bounds.</summary>
    Center,

    /// <summary>Aligns the label to the trailing edge of the padded content area.</summary>
    End,
}

/// <summary>
/// Represents a text button with an instance-owned nine-patch background and text component.
/// </summary>
public partial class TextButton : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];
    private readonly Texture _texture;
    private readonly ITextStyle _textStyle;
    private readonly ulong _backgroundRenderLayerMask;
    private readonly ulong _textRenderLayerMask;
    private readonly Vector4D<float> _sourceBorders;
    private readonly ISamplingBehavior _samplingBehavior;
    private NinePatchComponent? _background;
    private TextComponent? _text;
    private float _horizontalPadding;
    private float _verticalPadding;

    [Observable(PublicSetter = true)]
    private string _label = string.Empty;

    [Observable]
    private TextButtonLabelAlignment _labelAlignment = TextButtonLabelAlignment.Center;

    [Observable(PublicSetter = true)]
    private Action? _action;

    private void BeforeLabelChanges(string value) => ArgumentNullException.ThrowIfNull(value);

    private void BeforeLabelAlignmentChanges(TextButtonLabelAlignment value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    protected virtual partial void AfterLabelChanges(string previousValue)
    {
        if (_text is not null)
            _text.Text = Label;
    }

    /// <summary>
    /// Gets or sets the horizontal and vertical padding around the label.
    /// </summary>
    public Vector2D<float> Padding
    {
        get => new(_horizontalPadding, _verticalPadding);
        set
        {
            if (
                !float.IsFinite(value.X)
                || !float.IsFinite(value.Y)
                || value.X < 0f
                || value.Y < 0f
            )
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Padding values must be finite and non-negative."
                );

            if (_horizontalPadding == value.X && _verticalPadding == value.Y)
                return;

            _horizontalPadding = value.X;
            _verticalPadding = value.Y;
            NotifyPropertyChanged(nameof(Padding));
        }
    }

    /// <summary>
    /// Initializes a text button and creates its owned visual components.
    /// </summary>
    /// <param name="textStyle">The shared font and text style.</param>
    /// <param name="texture">The shared nine-patch texture.</param>
    /// <param name="horizontalPadding">The horizontal label padding.</param>
    /// <param name="verticalPadding">The vertical label padding.</param>
    /// <param name="backgroundRenderLayerMask">The render-layer mask for the background.</param>
    /// <param name="textRenderLayerMask">The render-layer mask for the text.</param>
    /// <param name="sourceBorders">The source texture border widths.</param>
    /// <param name="samplingBehavior">The texture sampling behavior.</param>
    public TextButton(
        ITextStyle textStyle,
        Texture texture,
        float horizontalPadding = 16f,
        float verticalPadding = 10f,
        ulong backgroundRenderLayerMask = ulong.MaxValue,
        ulong textRenderLayerMask = ulong.MaxValue,
        Vector4D<float>? sourceBorders = null,
        ISamplingBehavior? samplingBehavior = null
    )
        : base()
    {
        ArgumentNullException.ThrowIfNull(textStyle);
        ArgumentNullException.ThrowIfNull(texture);
        if (!float.IsFinite(horizontalPadding) || horizontalPadding < 0f)
            throw new ArgumentOutOfRangeException(nameof(horizontalPadding));
        if (!float.IsFinite(verticalPadding) || verticalPadding < 0f)
            throw new ArgumentOutOfRangeException(nameof(verticalPadding));

        _texture = texture;
        _textStyle = textStyle;
        _horizontalPadding = horizontalPadding;
        _verticalPadding = verticalPadding;
        _backgroundRenderLayerMask = backgroundRenderLayerMask;
        _textRenderLayerMask = textRenderLayerMask;
        _sourceBorders = sourceBorders ?? new Vector4D<float>(12f, 12f, 12f, 12f);
        _samplingBehavior = samplingBehavior ?? SamplingBehaviors.PixelPerfect;
        SetCanFocus(true);
        InputMap.OnMouseButtonReleased(MouseButtonEnum.Left).Invoke(InvokeAction);
        CreateVisualComponents();
    }

    /// <summary>Invokes the action assigned to this button, if any.</summary>
    private void InvokeAction() => Action?.Invoke();

    /// <summary>
    /// <summary>Creates fresh visual components from the button's retained configuration.</summary>
    private void CreateVisualComponents()
    {
        var background = new NinePatchComponent { };
        var text = new TextComponent(_textStyle);
        background.Texture = _texture;
        background.RenderLayerMask = _backgroundRenderLayerMask;
        background.SamplingBehavior = _samplingBehavior;
        background.SourceBorders = _sourceBorders;
        _background = background;
        _text = text;
        text.RenderLayerMask = _textRenderLayerMask;
        text.Text = Label;
        var labelSize = MeasureLabel(_textStyle, Label);
        AddComponent(background);
        AddComponent(text);
        if (Bounds.Size.X > 0f && Bounds.Size.Y > 0f)
            Arrange(Bounds);
    }

    /// <summary>Removes the current visual components and releases their references.</summary>
    private void RemoveVisualComponents()
    {
        var background = _background;
        var text = _text;
        _background = null;
        _text = null;

        if (text is not null)
            RemoveComponent(text);
        if (background is not null)
            RemoveComponent(background);
    }

    /// <summary>Synchronizes component ownership with effective visibility.</summary>
    private void UpdateVisualComponents()
    {
        if (IsEffectivelyVisible)
        {
            if (_background is null && _text is null)
                CreateVisualComponents();
        }
        else if (_background is not null || _text is not null)
        {
            RemoveVisualComponents();
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

    /// <summary>Updates components when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The name of the changed property.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            UpdateVisualComponents();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateVisualComponents();
    }

    /// <inheritdoc />
    protected override void AfterIsVisibleChanges() => UpdateVisualComponents();

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;

        var labelSize = MeasureLabel(_textStyle, Label);
        var desiredSize = new Vector2D<float>(
            MathF.Ceiling(labelSize.X) + _horizontalPadding * 2f,
            MathF.Ceiling(labelSize.Y) + _verticalPadding * 2f
        );

        return new(MathF.Min(desiredSize.X, constraint.X), MathF.Min(desiredSize.Y, constraint.Y));
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        if (!IsEffectivelyVisible || _text is null || _background is null)
            return;

        base.Arrange(bounds);

        var labelWidth = MathF.Max(0f, bounds.Size.X - _horizontalPadding * 2f);
        var visibleLabel = FitTextToWidth(_textStyle, Label, labelWidth);
        if (_text.Text != visibleLabel)
            _text.Text = visibleLabel;

        var horizontalAlignment = LabelAlignment switch
        {
            TextButtonLabelAlignment.Start => 0f,
            TextButtonLabelAlignment.Center => 0.5f,
            TextButtonLabelAlignment.End => 1f,
            _ => throw new InvalidOperationException("Unknown label alignment."),
        };
        var destination = LabelAlignment switch
        {
            TextButtonLabelAlignment.Start => new Rectangle<float>(
                bounds.Origin.X + _horizontalPadding,
                bounds.Origin.Y,
                MathF.Max(0f, bounds.Size.X - _horizontalPadding),
                bounds.Size.Y
            ),
            TextButtonLabelAlignment.End => new Rectangle<float>(
                bounds.Origin.X,
                bounds.Origin.Y,
                MathF.Max(0f, bounds.Size.X - _horizontalPadding),
                bounds.Size.Y
            ),
            _ => bounds,
        };
        _text.Destination = destination;
        _text.Alignment = new Vector2D<float>(horizontalAlignment, 0.5f);
        var textBounds = _text.LayoutBounds;
        var textOrigin = new Vector2D<float>(
            MathF.Round(textBounds.Origin.X),
            MathF.Round(textBounds.Origin.Y)
        );
        SetPosition(textOrigin);
        _background.Destination = bounds;
    }

    /// <summary>
    /// Measures the combined visible bounds of newline-separated label spans.
    /// </summary>
    /// <param name="style">The font metrics used to measure the label.</param>
    /// <param name="label">The complete label.</param>
    /// <returns>The combined glyph bounds size.</returns>
    private static Vector2D<float> MeasureLabel(ITextStyle style, string label)
    {
        if (label.Length == 0)
            return Vector2D<float>.Zero;

        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        var lines = label.Split('\n');
        var top = float.PositiveInfinity;
        var bottom = float.NegativeInfinity;
        var width = 0f;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var bounds = MeasureTextBounds(style, lines[lineIndex]);
            width = MathF.Max(width, bounds.Size.X);
            top = MathF.Min(top, bounds.Origin.Y + lineIndex * lineHeight);
            bottom = MathF.Max(bottom, bounds.Max.Y + lineIndex * lineHeight);
        }

        return float.IsFinite(top) && float.IsFinite(bottom)
            ? new Vector2D<float>(width, bottom - top)
            : Vector2D<float>.Zero;
    }

    /// <summary>
    /// Returns the longest leading rune sequence that fits within the available width.
    /// </summary>
    /// <param name="style">The font metrics used to measure the label.</param>
    /// <param name="label">The complete label to fit.</param>
    /// <param name="availableWidth">The maximum visible width.</param>
    /// <returns>The fitting label prefix.</returns>
    private static string FitTextToWidth(ITextStyle style, string label, float availableWidth)
    {
        var prefix = new StringBuilder();
        foreach (var rune in label.EnumerateRunes())
        {
            var candidate = prefix.ToString() + rune;
            if (MeasureTextBounds(style, candidate).Size.X > availableWidth)
                break;

            prefix.Append(rune);
        }

        return prefix.ToString();
    }

    /// <summary>Measures text by preparing it with a temporary graphics component.</summary>
    /// <param name="style">The font and visual style used by the text.</param>
    /// <param name="text">The text to measure.</param>
    /// <returns>The glyph bounds in text-local coordinates.</returns>
    private static Rectangle<float> MeasureTextBounds(ITextStyle style, string text)
    {
        var component = new TextComponent(style) { Text = text };
        return component.LayoutBounds;
    }
}
