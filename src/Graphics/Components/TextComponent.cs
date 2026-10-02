namespace Nexus.Graphics.Components;

/// <summary>
/// Groups text spans that share the component's lifetime.
/// </summary>
public partial class TextComponent : Component, IGraphicsComponent
{
    private readonly List<(TextSpan Span, float VerticalOffset)> _spans = [];
    private readonly ITextStyle _textStyle;

    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    [Observable(PublicSetter = true)]
    private Vector2D<float> _position;
    private string _text = string.Empty;

    /// <inheritdoc />
    public override string DisplayName => "Text";

    /// <summary>Updates spans after the render-layer mask changes.</summary>
    private void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        foreach (var (span, _) in _spans)
            span.RenderLayerMask = RenderLayerMask;
    }

    /// <inheritdoc/>
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc/>
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <summary>Initializes a text component with the style used by its spans.</summary>
    /// <param name="textStyle">The font and visual data used to render the component's text.</param>
    public TextComponent(ITextStyle textStyle)
    {
        _textStyle = textStyle ?? throw new ArgumentNullException(nameof(textStyle));
    }

    /// <summary>Updates spans after the component position changes.</summary>
    private void AfterPositionChanges(Vector2D<float> previousValue) =>
        UpdateTransformationMatrix();

    /// <summary>Gets or sets the text represented by this component.</summary>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value)
                return;

            _text = value;

            var removedSpans = _spans.Select(item => item.Span).ToArray();
            _spans.Clear();

            var scale =
                _textStyle.FontMetrics.EmSize == 0
                    ? 1.0
                    : _textStyle.Size / _textStyle.FontMetrics.EmSize;
            var lineHeight = (float)(_textStyle.FontMetrics.LineHeight * scale);
            var lines = _text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var span = new TextSpan(_textStyle, lines[lineIndex])
                {
                    RenderLayerMask = RenderLayerMask,
                };
                if (((IDrawable)span).InstanceCount == 0)
                    continue;

                var verticalOffset = lineIndex * lineHeight;
                span.TransformationMatrix = CreateSpanTransformation(verticalOffset);
                _spans.Add((span, verticalOffset));
            }

            foreach (var removedSpan in removedSpans)
                DrawableRemoved?.Invoke(this, new DrawableEventArgs(removedSpan));

            foreach (var (span, _) in _spans)
                DrawableAdded?.Invoke(this, new DrawableEventArgs(span));

            OnPropertyChanged(nameof(Text));
        }
    }

    /// <summary>Gets the spans as drawable contributions for the graphics system.</summary>
    public IReadOnlyList<IDrawable> Drawables =>
        _spans.Select(item => (IDrawable)item.Span).ToArray();

    /// <summary>Gets the combined visible glyph bounds for all text lines.</summary>
    public Rectangle<float> LayoutBounds
    {
        get
        {
            if (_spans.Count == 0)
                return new Rectangle<float>(0f, 0f, 0f, 0f);

            var left = float.PositiveInfinity;
            var top = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            var bottom = float.NegativeInfinity;
            foreach (var (span, verticalOffset) in _spans)
            {
                var bounds = span.LayoutBounds;
                left = MathF.Min(left, bounds.Origin.X);
                top = MathF.Min(top, bounds.Origin.Y + verticalOffset);
                right = MathF.Max(right, bounds.Max.X);
                bottom = MathF.Max(bottom, bounds.Max.Y + verticalOffset);
            }

            return new Rectangle<float>(left, top, right - left, bottom - top);
        }
    }

    /// <summary>Updates every span from the explicit text origin and its line offset.</summary>
    private void UpdateTransformationMatrix()
    {
        foreach (var (span, verticalOffset) in _spans)
            span.TransformationMatrix = CreateSpanTransformation(verticalOffset);
    }

    /// <summary>Creates a span transform from the explicit point and line offset.</summary>
    /// <param name="verticalOffset">The line's scaled vertical offset.</param>
    /// <returns>The transform used by the span's glyph instances.</returns>
    private Matrix4X4<float> CreateSpanTransformation(float verticalOffset) =>
        Matrix4X4.CreateTranslation(Position.X, Position.Y + verticalOffset, 0f);
}
