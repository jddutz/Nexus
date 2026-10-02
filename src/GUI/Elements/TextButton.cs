namespace Nexus.GUI.Elements;

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
    private Rectangle<float>? _layoutBounds;
    private float _horizontalPadding;
    private float _verticalPadding;

    [Observable(PublicSetter = true)]
    private string _label = string.Empty;

    [Observable]
    private TextButtonLabelAlignment _labelAlignment = TextButtonLabelAlignment.Center;

    [Observable(PublicSetter = true)]
    private Action? _action;

    /// <summary>Rejects a null label value.</summary>
    /// <param name="value">The proposed label.</param>
    private void BeforeLabelChanges(string value) => ArgumentNullException.ThrowIfNull(value);

    /// <summary>Validates the proposed label alignment.</summary>
    /// <param name="value">The proposed label alignment.</param>
    private void BeforeLabelAlignmentChanges(TextButtonLabelAlignment value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Updates the text component after the button label changes.</summary>
    /// <param name="previousValue">The previous label.</param>
    protected virtual partial void AfterLabelChanges(string previousValue)
    {
        if (_text is not null)
            _text.Text = Label;
    }

    /// <summary>Reapplies layout when label alignment changes.</summary>
    /// <param name="previousValue">The previous label alignment.</param>
    protected virtual partial void AfterLabelAlignmentChanges(
        TextButtonLabelAlignment previousValue
    ) => ReapplyLayout();

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
            ReapplyLayout();
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

    /// <summary>Creates fresh visual components from the button's retained configuration.</summary>
    private void CreateVisualComponents()
    {
        var background = new NinePatchComponent { };
        var text = new TextComponent(_textStyle)
        {
            RenderLayerMask = _textRenderLayerMask,
            Text = Label,
            Wrap = false,
            MaximumLines = 1,
        };
        background.Texture = _texture;
        background.RenderLayerMask = _backgroundRenderLayerMask;
        background.SamplingBehavior = _samplingBehavior;
        background.SourceBorders = _sourceBorders;
        _background = background;
        _text = text;
        AddComponent(background);
        AddComponent(text);
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
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

        if (
            float.IsNaN(constraint.X)
            || constraint.X < 0f
            || float.IsNaN(constraint.Y)
            || constraint.Y < 0f
        )
            throw new ArgumentOutOfRangeException(nameof(constraint));

        var textConstraint = new Vector2D<float>(
            MathF.Max(0f, constraint.X - 2f * _horizontalPadding),
            MathF.Max(0f, constraint.Y - 2f * _verticalPadding)
        );
        var labelSize = _text?.Measure(textConstraint) ?? Vector2D<float>.Zero;
        return new(
            MathF.Min(labelSize.X + _horizontalPadding * 2f, constraint.X),
            MathF.Min(labelSize.Y + _verticalPadding * 2f, constraint.Y)
        );
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        _layoutBounds = bounds;
        base.Arrange(bounds);
        if (!IsEffectivelyVisible || _text is null || _background is null)
            return;

        var horizontalAlignment = LabelAlignment switch
        {
            TextButtonLabelAlignment.Start => 0f,
            TextButtonLabelAlignment.Center => 0.5f,
            TextButtonLabelAlignment.End => 1f,
            _ => throw new InvalidOperationException("Unknown label alignment."),
        };
        _text.Text = Label;
        _text.Wrap = false;
        _text.MaximumLines = 1;
        _text.Destination = new Rectangle<float>(
            bounds.Origin.X + _horizontalPadding,
            bounds.Origin.Y + _verticalPadding,
            MathF.Max(0f, bounds.Size.X - 2f * _horizontalPadding),
            MathF.Max(0f, bounds.Size.Y - 2f * _verticalPadding)
        );
        _text.Alignment = new Vector2D<float>(horizontalAlignment, 0.5f);
        _background.Destination = bounds;
    }

    /// <summary>Reapplies the most recently assigned bounds to the active visuals.</summary>
    private void ReapplyLayout()
    {
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }
}
