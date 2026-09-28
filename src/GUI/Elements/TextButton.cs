namespace Nexus.GUI.Elements;

using System.Text;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.Input.Events;

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
    private InputDeviceId? _activePointerId;
    private readonly HashSet<InputDeviceId> _insidePointers = [];
    private readonly Dictionary<InputDeviceId, Vector2D<float>> _pointerPositions = [];

    /// <summary>Occurs when a pointer enters the button bounds.</summary>
    public event EventHandler<PointerEventArgs>? PointerEntered;

    /// <summary>Occurs when a pointer exits the button bounds.</summary>
    public event EventHandler<PointerEventArgs>? PointerExited;

    /// <summary>Occurs when an eligible pointer press begins inside the button.</summary>
    public event EventHandler<PointerEventArgs>? Pressed;

    /// <summary>Occurs when the active pointer press ends normally.</summary>
    public event EventHandler<PointerReleasedEventArgs>? Released;

    /// <summary>Occurs after a press is released inside the button.</summary>
    public event EventHandler<PointerEventArgs>? Activated;

    /// <summary>Occurs when the active pointer press is interrupted.</summary>
    public event EventHandler<PointerEventArgs>? Canceled;

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
        InputMap.PointerMoved += OnInputPointerMoved;
        InputMap.PointerPressed += OnInputPointerPressed;
        InputMap.PointerReleased += OnInputPointerReleased;
        InputMap.PointerDisconnected += OnInputPointerDisconnected;
        InputMap.PointerCanceled += OnInputPointerCanceled;
    }

    /// <inheritdoc />
    internal override void OnPointerEntered(PointerEventArgs eventArgs)
    {
        _pointerPositions[eventArgs.PointerId] = eventArgs.Position;
        if (_insidePointers.Add(eventArgs.PointerId))
            PointerEntered?.Invoke(this, eventArgs);
    }

    /// <inheritdoc />
    internal override void OnPointerExited(PointerEventArgs eventArgs)
    {
        var wasInside = _insidePointers.Remove(eventArgs.PointerId);
        if (_activePointerId == eventArgs.PointerId)
            _pointerPositions[eventArgs.PointerId] = eventArgs.Position;
        else
            _pointerPositions.Remove(eventArgs.PointerId);

        if (wasInside)
            PointerExited?.Invoke(this, eventArgs);
    }

    /// <inheritdoc />
    internal override bool TryPointerDown(PointerEventArgs eventArgs)
    {
        if (_activePointerId.HasValue || !ContainsPointerPosition(eventArgs.Position))
            return false;

        _activePointerId = eventArgs.PointerId;
        _pointerPositions[eventArgs.PointerId] = eventArgs.Position;
        _insidePointers.Add(eventArgs.PointerId);
        Pressed?.Invoke(this, eventArgs);
        return true;
    }

    /// <inheritdoc />
    internal override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        if (_activePointerId == eventArgs.PointerId)
        {
            _pointerPositions[eventArgs.PointerId] = eventArgs.Position;
            UpdateActivePointerInside(eventArgs);
        }
    }

    /// <inheritdoc />
    internal override void OnPointerUp(PointerEventArgs eventArgs)
    {
        if (_activePointerId != eventArgs.PointerId)
            return;

        var isInside = ContainsPointerPosition(eventArgs.Position);
        UpdateActivePointerInside(eventArgs, isInside);
        _activePointerId = null;
        if (!isInside)
            _pointerPositions.Remove(eventArgs.PointerId);
        Released?.Invoke(
            this,
            new PointerReleasedEventArgs(
                eventArgs.PointerId,
                eventArgs.Position,
                isInside,
                eventArgs.Button
            )
        );
        if (isInside)
            Activated?.Invoke(this, eventArgs);
    }

    /// <inheritdoc />
    internal override void OnPointerCanceled(PointerEventArgs eventArgs)
    {
        if (_activePointerId != eventArgs.PointerId)
            return;

        _activePointerId = null;
        Canceled?.Invoke(this, eventArgs);
    }

    /// <inheritdoc />
    internal override void CancelPointerInput()
    {
        if (_activePointerId is { } activePointerId)
        {
            var position = _pointerPositions.GetValueOrDefault(activePointerId);
            OnPointerCanceled(new PointerEventArgs(activePointerId, position));
        }

        foreach (var pointerId in _insidePointers.ToArray())
        {
            var position = _pointerPositions.GetValueOrDefault(pointerId);
            OnPointerExited(new PointerEventArgs(pointerId, position));
        }

        _pointerPositions.Clear();
    }

    /// <summary>Receives raw movement and updates this button's own hover and press state.</summary>
    /// <param name="message">The mouse movement event.</param>
    private void OnInputPointerMoved(MouseMovedEvent message)
    {
        var pointerId = message.Mouse?.Id ?? InputDeviceId.Invalid;
        var eventArgs = new PointerEventArgs(pointerId, message.Position);
        var isInside = ContainsPointerPosition(message.Position);
        if (isInside)
            OnPointerEntered(eventArgs);
        else
            OnPointerExited(eventArgs);

        OnPointerMoved(eventArgs);
    }

    /// <summary>Receives raw presses and accepts only primary-button presses within current bounds.</summary>
    /// <param name="message">The mouse-button press event.</param>
    private void OnInputPointerPressed(MouseButtonPressedEvent message)
    {
        if (message.Button != MouseButtonEnum.Left)
            return;

        var pointerId = message.Mouse?.Id ?? InputDeviceId.Invalid;
        var eventArgs = new PointerEventArgs(pointerId, message.Position, message.Button);
        if (ContainsPointerPosition(message.Position))
            OnPointerEntered(eventArgs);
        TryPointerDown(eventArgs);
    }

    /// <summary>Receives raw releases and only releases this button's captured pointer.</summary>
    /// <param name="message">The mouse-button release event.</param>
    private void OnInputPointerReleased(MouseButtonReleasedEvent message)
    {
        if (message.Button != MouseButtonEnum.Left)
            return;

        var pointerId = message.Mouse?.Id ?? InputDeviceId.Invalid;
        var eventArgs = new PointerEventArgs(pointerId, message.Position, message.Button);
        OnPointerMoved(eventArgs);
        OnPointerUp(eventArgs);
    }

    /// <summary>Cancels this button's press when its pointer device disconnects.</summary>
    /// <param name="message">The mouse-disconnection event.</param>
    private void OnInputPointerDisconnected(MouseDisconnectedEvent message)
    {
        var eventArgs = new PointerEventArgs(message.Mouse.Id, message.Position);
        OnPointerCanceled(eventArgs);
        OnPointerExited(eventArgs);
        _pointerPositions.Remove(message.Mouse.Id);
    }

    /// <summary>Cancels this button's press and hover when the application loses focus.</summary>
    /// <param name="message">The mouse-cancellation event.</param>
    private void OnInputPointerCanceled(MouseCanceledEvent message)
    {
        var eventArgs = new PointerEventArgs(message.Mouse.Id, message.Position);
        OnPointerCanceled(eventArgs);
        OnPointerExited(eventArgs);
        _pointerPositions.Remove(message.Mouse.Id);
    }

    /// <summary>Updates the active pointer's inside state and emits a crossing action.</summary>
    /// <param name="eventArgs">The current pointer event data.</param>
    /// <param name="isInside">The current inside state, or null to test current bounds.</param>
    private void UpdateActivePointerInside(PointerEventArgs eventArgs, bool? isInside = null)
    {
        var nowInside = isInside ?? ContainsPointerPosition(eventArgs.Position);
        var wasInside = _insidePointers.Contains(eventArgs.PointerId);
        if (wasInside == nowInside)
            return;

        if (nowInside)
        {
            _insidePointers.Add(eventArgs.PointerId);
            PointerEntered?.Invoke(this, eventArgs);
        }
        else
        {
            _insidePointers.Remove(eventArgs.PointerId);
            PointerExited?.Invoke(this, eventArgs);
        }
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
