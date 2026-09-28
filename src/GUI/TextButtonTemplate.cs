namespace Nexus.GUI;

using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;

/// <summary>
/// Creates text buttons with a shared visual definition and fresh instance components.
/// </summary>
public sealed class TextButtonTemplate : ITemplate<Element>
{
    private readonly ulong _backgroundRenderLayerMask;
    private readonly float _horizontalPadding;
    private readonly string _defaultLabel;
    private readonly Vector4D<float> _sourceBorders;
    private readonly ISamplingBehavior _samplingBehavior;
    private readonly ITextStyle _textStyle;
    private readonly Texture _texture;
    private readonly ulong _textRenderLayerMask;
    private readonly float _verticalPadding;

    /// <summary>
    /// Gets the label used when creating an instance without an initializer.
    /// </summary>
    public string DefaultLabel => _defaultLabel;

    /// <summary>
    /// Initializes a template with reusable button style and layout defaults.
    /// </summary>
    /// <param name="textStyle">The shared font and text style.</param>
    /// <param name="texture">The shared nine-patch texture.</param>
    /// <param name="defaultLabel">The label assigned to each new button.</param>
    /// <param name="horizontalPadding">The horizontal label padding.</param>
    /// <param name="verticalPadding">The vertical label padding.</param>
    /// <param name="backgroundRenderLayerMask">The render-layer mask for the background.</param>
    /// <param name="textRenderLayerMask">The render-layer mask for the text.</param>
    /// <param name="sourceBorders">The source texture border widths.</param>
    /// <param name="samplingBehavior">The texture sampling behavior.</param>
    public TextButtonTemplate(
        ITextStyle textStyle,
        Texture texture,
        string defaultLabel,
        float horizontalPadding = 16f,
        float verticalPadding = 10f,
        ulong backgroundRenderLayerMask = ulong.MaxValue,
        ulong textRenderLayerMask = ulong.MaxValue,
        Vector4D<float>? sourceBorders = null,
        ISamplingBehavior? samplingBehavior = null
    )
    {
        ArgumentNullException.ThrowIfNull(textStyle);
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(defaultLabel);
        if (!float.IsFinite(horizontalPadding) || horizontalPadding < 0f)
            throw new ArgumentOutOfRangeException(nameof(horizontalPadding));
        if (!float.IsFinite(verticalPadding) || verticalPadding < 0f)
            throw new ArgumentOutOfRangeException(nameof(verticalPadding));

        _textStyle = textStyle;
        _texture = texture;
        _defaultLabel = defaultLabel;
        _horizontalPadding = horizontalPadding;
        _verticalPadding = verticalPadding;
        _backgroundRenderLayerMask = backgroundRenderLayerMask;
        _textRenderLayerMask = textRenderLayerMask;
        _sourceBorders = sourceBorders ?? new Vector4D<float>(12f, 12f, 12f, 12f);
        _samplingBehavior = samplingBehavior ?? SamplingBehaviors.PixelPerfect;
    }

    /// <inheritdoc/>
    public Element Create(Action<Element>? initializer = null)
    {
        var background = new NinePatchComponent
        {
            Texture = _texture,
            RenderLayerMask = _backgroundRenderLayerMask,
            SamplingBehavior = _samplingBehavior,
            SourceBorders = _sourceBorders,
        };
        var text = new TextComponent(_textStyle)
        {
            RenderLayerMask = _textRenderLayerMask,
            Text = _defaultLabel,
        };
        var labelSize = text.LayoutBounds.Size;
        background.Size = new Vector2D<float>(
            MathF.Max(float.Epsilon, MathF.Ceiling(labelSize.X) + _horizontalPadding * 2f),
            MathF.Max(float.Epsilon, MathF.Ceiling(labelSize.Y) + _verticalPadding * 2f)
        );
        var layout = new ButtonLayout(
            _textStyle,
            text,
            background,
            _defaultLabel,
            _horizontalPadding,
            _verticalPadding
        );
        var element = new Element(
            measure: (_, availableSize) => layout.Measure(availableSize),
            arrange: (arrangedElement, bounds) => layout.Arrange(arrangedElement, bounds),
            components: [background, text]
        );

        initializer?.Invoke(element);
        layout.SetInitializerLabel(text.Text);
        return element;
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
        var prefix = new System.Text.StringBuilder();
        foreach (var rune in label.EnumerateRunes())
        {
            var candidate = prefix.ToString() + rune;
            if (new TextSpan(style, candidate).LayoutBounds.Size.X > availableWidth)
                break;

            prefix.Append(rune);
        }

        return prefix.ToString();
    }

    /// <summary>
    /// Stores the mutable layout state and component references for one element instance.
    /// </summary>
    private sealed class ButtonLayout
    {
        private readonly NinePatchComponent _background;
        private readonly float _horizontalPadding;
        private readonly TextComponent _text;
        private readonly ITextStyle _textStyle;
        private readonly float _verticalPadding;
        private string _label;
        private string _lastDisplayedLabel;

        /// <summary>
        /// Initializes per-element layout state without retaining it in the template.
        /// </summary>
        /// <param name="textStyle">The style used to measure and display the label.</param>
        /// <param name="text">The instance-owned text component.</param>
        /// <param name="background">The instance-owned nine-patch component.</param>
        /// <param name="label">The complete starting label.</param>
        /// <param name="horizontalPadding">The horizontal label padding.</param>
        /// <param name="verticalPadding">The vertical label padding.</param>
        public ButtonLayout(
            ITextStyle textStyle,
            TextComponent text,
            NinePatchComponent background,
            string label,
            float horizontalPadding,
            float verticalPadding
        )
        {
            _textStyle = textStyle;
            _text = text;
            _background = background;
            _label = label;
            _lastDisplayedLabel = label;
            _horizontalPadding = horizontalPadding;
            _verticalPadding = verticalPadding;
        }

        /// <summary>
        /// Measures the complete label and padding within the available size.
        /// </summary>
        /// <param name="availableSize">The available size constraint.</param>
        /// <returns>The measured element size.</returns>
        public Vector2D<float> Measure(Vector2D<float> availableSize)
        {
            UpdateLabelFromComponent();
            var labelSize = MeasureLabel(_textStyle, _label);
            var desiredSize = new Vector2D<float>(
                MathF.Ceiling(labelSize.X) + _horizontalPadding * 2f,
                MathF.Ceiling(labelSize.Y) + _verticalPadding * 2f
            );

            return new(
                MathF.Min(desiredSize.X, availableSize.X),
                MathF.Min(desiredSize.Y, availableSize.Y)
            );
        }

        /// <summary>
        /// Sets the source label after the caller's initializer has run.
        /// </summary>
        /// <param name="label">The label left on the instance text component.</param>
        public void SetInitializerLabel(string label)
        {
            _label = label;
            _lastDisplayedLabel = label;
        }

        /// <summary>
        /// Arranges the background over the full element bounds and centers fitted glyphs.
        /// </summary>
        /// <param name="element">The plain element being arranged.</param>
        /// <param name="bounds">The complete button and hit-area bounds.</param>
        public void Arrange(Element element, Rectangle<float> bounds)
        {
            UpdateLabelFromComponent();
            element.Bounds = bounds;

            var labelWidth = MathF.Max(0f, bounds.Size.X - _horizontalPadding * 2f);
            var visibleLabel = FitTextToWidth(_textStyle, _label, labelWidth);
            if (_text.Text != visibleLabel)
                _text.Text = visibleLabel;
            _lastDisplayedLabel = visibleLabel;

            var textBounds = _text.LayoutBounds;
            var textOrigin = new Vector2D<float>(
                MathF.Round(bounds.Origin.X + (bounds.Size.X - textBounds.Size.X) / 2f),
                MathF.Round(bounds.Origin.Y + (bounds.Size.Y - textBounds.Size.Y) / 2f)
            );
            element.Position = new(
                textOrigin.X - textBounds.Origin.X,
                textOrigin.Y - textBounds.Origin.Y
            );
            _background.Size = bounds.Size;
            _background.TransformationMatrix = Matrix4X4.CreateTranslation(
                bounds.Origin.X - element.Position.X,
                bounds.Origin.Y - element.Position.Y,
                0f
            );
        }

        /// <summary>
        /// Tracks label edits made through the element's existing text component.
        /// </summary>
        private void UpdateLabelFromComponent()
        {
            if (_text.Text == _lastDisplayedLabel)
                return;

            _label = _text.Text;
            _lastDisplayedLabel = _text.Text;
        }
    }
}
