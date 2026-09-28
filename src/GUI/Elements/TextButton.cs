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
public sealed class TextButton : Element
{
    private readonly NinePatchComponent _background;
    private readonly TextComponent _text;
    private readonly ITextStyle _textStyle;
    private float _horizontalPadding;
    private float _verticalPadding;
    private string _label;
    private TextButtonLabelAlignment _labelAlignment = TextButtonLabelAlignment.Center;

    /// <summary>
    /// Gets or sets the complete label, before any width-based display fitting.
    /// </summary>
    public string Label
    {
        get => _label;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!SetProperty(ref _label, value))
                return;

            _text.Text = value;
        }
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
            OnPropertyChanged(nameof(Padding));
        }
    }

    /// <summary>
    /// Gets or sets the horizontal alignment of the label inside the padded area.
    /// </summary>
    public TextButtonLabelAlignment LabelAlignment
    {
        get => _labelAlignment;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            SetProperty(ref _labelAlignment, value);
        }
    }

    /// <summary>
    /// Initializes a text button and creates its owned components.
    /// </summary>
    /// <param name="label">The complete label shown by the button.</param>
    /// <param name="textStyle">The shared font and text style.</param>
    /// <param name="texture">The shared nine-patch texture.</param>
    /// <param name="horizontalPadding">The horizontal label padding.</param>
    /// <param name="verticalPadding">The vertical label padding.</param>
    /// <param name="backgroundRenderLayerMask">The render-layer mask for the background.</param>
    /// <param name="textRenderLayerMask">The render-layer mask for the text.</param>
    /// <param name="sourceBorders">The source texture border widths.</param>
    /// <param name="samplingBehavior">The texture sampling behavior.</param>
    public TextButton(
        string label,
        ITextStyle textStyle,
        Texture texture,
        float horizontalPadding = 16f,
        float verticalPadding = 10f,
        ulong backgroundRenderLayerMask = ulong.MaxValue,
        ulong textRenderLayerMask = ulong.MaxValue,
        Vector4D<float>? sourceBorders = null,
        ISamplingBehavior? samplingBehavior = null
    )
        : this(
            CreateComponents(
                label,
                textStyle,
                texture,
                horizontalPadding,
                verticalPadding,
                backgroundRenderLayerMask,
                textRenderLayerMask,
                sourceBorders,
                samplingBehavior
            )
        ) { }

    /// <summary>
    /// Initializes the base element from components and layout created for this instance.
    /// </summary>
    /// <param name="composition">The fresh components and layout state for this button.</param>
    private TextButton(
        (
            NinePatchComponent Background,
            TextComponent Text,
            ITextStyle TextStyle,
            string Label,
            float HorizontalPadding,
            float VerticalPadding
        ) composition
    )
        : base(components: [composition.Background, composition.Text])
    {
        _background = composition.Background;
        _text = composition.Text;
        _textStyle = composition.TextStyle;
        _label = composition.Label;
        _horizontalPadding = composition.HorizontalPadding;
        _verticalPadding = composition.VerticalPadding;
    }

    /// <summary>
    /// Creates fresh button components and their instance-specific layout state.
    /// </summary>
    /// <param name="label">The complete label shown by the button.</param>
    /// <param name="textStyle">The shared font and text style.</param>
    /// <param name="texture">The shared nine-patch texture.</param>
    /// <param name="horizontalPadding">The horizontal label padding.</param>
    /// <param name="verticalPadding">The vertical label padding.</param>
    /// <param name="backgroundRenderLayerMask">The render-layer mask for the background.</param>
    /// <param name="textRenderLayerMask">The render-layer mask for the text.</param>
    /// <param name="sourceBorders">The source texture border widths.</param>
    /// <param name="samplingBehavior">The texture sampling behavior.</param>
    /// <returns>The new components and immutable layout configuration.</returns>
    private static (
        NinePatchComponent Background,
        TextComponent Text,
        ITextStyle TextStyle,
        string Label,
        float HorizontalPadding,
        float VerticalPadding
    ) CreateComponents(
        string label,
        ITextStyle textStyle,
        Texture texture,
        float horizontalPadding,
        float verticalPadding,
        ulong backgroundRenderLayerMask,
        ulong textRenderLayerMask,
        Vector4D<float>? sourceBorders,
        ISamplingBehavior? samplingBehavior
    )
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(textStyle);
        ArgumentNullException.ThrowIfNull(texture);
        if (!float.IsFinite(horizontalPadding) || horizontalPadding < 0f)
            throw new ArgumentOutOfRangeException(nameof(horizontalPadding));
        if (!float.IsFinite(verticalPadding) || verticalPadding < 0f)
            throw new ArgumentOutOfRangeException(nameof(verticalPadding));

        var background = new NinePatchComponent
        {
            Texture = texture,
            RenderLayerMask = backgroundRenderLayerMask,
            SamplingBehavior = samplingBehavior ?? SamplingBehaviors.PixelPerfect,
            SourceBorders = sourceBorders ?? new Vector4D<float>(12f, 12f, 12f, 12f),
        };
        var text = new TextComponent(textStyle)
        {
            RenderLayerMask = textRenderLayerMask,
            Text = label,
        };
        var labelSize = MeasureLabel(textStyle, label);
        background.Size = new Vector2D<float>(
            MathF.Max(float.Epsilon, MathF.Ceiling(labelSize.X) + horizontalPadding * 2f),
            MathF.Max(float.Epsilon, MathF.Ceiling(labelSize.Y) + verticalPadding * 2f)
        );
        return (background, text, textStyle, label, horizontalPadding, verticalPadding);
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        var labelSize = MeasureLabel(_textStyle, _label);
        var desiredSize = new Vector2D<float>(
            MathF.Ceiling(labelSize.X) + _horizontalPadding * 2f,
            MathF.Ceiling(labelSize.Y) + _verticalPadding * 2f
        );

        return new(MathF.Min(desiredSize.X, constraint.X), MathF.Min(desiredSize.Y, constraint.Y));
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        base.Arrange(bounds);

        var labelWidth = MathF.Max(0f, bounds.Size.X - _horizontalPadding * 2f);
        var visibleLabel = FitTextToWidth(_textStyle, _label, labelWidth);
        if (_text.Text != visibleLabel)
            _text.Text = visibleLabel;

        var textBounds = _text.LayoutBounds;
        var textX = _labelAlignment switch
        {
            TextButtonLabelAlignment.Start => bounds.Origin.X + _horizontalPadding,
            TextButtonLabelAlignment.Center => bounds.Origin.X
                + (bounds.Size.X - textBounds.Size.X) / 2f,
            TextButtonLabelAlignment.End => bounds.Max.X - _horizontalPadding - textBounds.Size.X,
            _ => throw new InvalidOperationException("Unknown label alignment."),
        };
        var textOrigin = new Vector2D<float>(
            MathF.Round(textX),
            MathF.Round(bounds.Origin.Y + (bounds.Size.Y - textBounds.Size.Y) / 2f)
        );
        Position = new(textOrigin.X - textBounds.Origin.X, textOrigin.Y - textBounds.Origin.Y);
        _background.Size = bounds.Size;
        _background.TransformationMatrix = Matrix4X4.CreateTranslation(
            bounds.Origin.X - Position.X,
            bounds.Origin.Y - Position.Y,
            0f
        );
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
            var bounds = new TextSpan(style, lines[lineIndex]).LayoutBounds;
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
            if (new TextSpan(style, candidate).LayoutBounds.Size.X > availableWidth)
                break;

            prefix.Append(rune);
        }

        return prefix.ToString();
    }
}
