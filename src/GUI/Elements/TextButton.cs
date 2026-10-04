namespace Nexus.GUI.Elements;

using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;

/// <summary>
/// Represents a text button with an instance-owned nine-patch background and text component.
/// </summary>
public partial class TextButton : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];
    private readonly Texture _texture;
    private readonly ITextStyle _textStyle;
    private readonly Vector4D<float> _sourceBorders;
    private readonly ISamplingBehavior _samplingBehavior;
    private NinePatchComponent? _background;
    private TextComponent? _text;

    /// <summary>Gets or sets the render-layer mask shared by the button visuals.</summary>
    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = ulong.MaxValue;

    [Observable(PublicSetter = true)]
    private string _label = string.Empty;

    [Observable(PublicSetter = true)]
    private Margins _padding = new();

    [Observable(PublicSetter = true)]
    private Action? _action;

    /// <summary>Rejects a null label value.</summary>
    /// <param name="value">The proposed label.</param>
    private void BeforeLabelChanges(string value) => ArgumentNullException.ThrowIfNull(value);

    /// <summary>Updates both visual components after the render-layer mask changes.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_background is not null)
            _background.RenderLayerMask = RenderLayerMask;
        if (_text is not null)
            _text.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Updates the text component after the button label changes.</summary>
    /// <param name="previousValue">The previous label.</param>
    protected virtual partial void AfterLabelChanges(string previousValue)
    {
        if (_text is not null)
            _text.Text = Label;
    }

    /// <summary>
    /// Initializes a text button and creates its owned visual components.
    /// </summary>
    /// <param name="textStyle">The shared font and text style.</param>
    /// <param name="texture">The shared nine-patch texture.</param>
    /// <param name="horizontalPadding">The horizontal label padding.</param>
    /// <param name="verticalPadding">The vertical label padding.</param>
    /// <param name="renderLayerMask">The render-layer mask shared by the background and text.</param>
    /// <param name="sourceBorders">The source texture border widths.</param>
    /// <param name="samplingBehavior">The texture sampling behavior.</param>
    public TextButton(
        ITextStyle textStyle,
        Texture texture,
        float horizontalPadding = 16f,
        float verticalPadding = 10f,
        ulong renderLayerMask = ulong.MaxValue,
        Vector4D<float>? sourceBorders = null,
        ISamplingBehavior? samplingBehavior = null
    )
        : base()
    {
        ArgumentNullException.ThrowIfNull(textStyle);
        ArgumentNullException.ThrowIfNull(texture);

        _texture = texture;
        _textStyle = textStyle;
        Padding = new(horizontalPadding, verticalPadding);
        RenderLayerMask = renderLayerMask;
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
            RenderLayerMask = RenderLayerMask,
            Text = Label,
            Wrap = false,
            MaximumLines = 1,
        };
        background.Texture = _texture;
        background.RenderLayerMask = RenderLayerMask;
        background.SamplingBehavior = _samplingBehavior;
        background.SourceBorders = _sourceBorders;
        _background = background;
        _text = text;
        AddComponent(background);
        AddComponent(text);
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
            return;
        else
        {
            if (_background is not null || _text is not null)
                RemoveVisualComponents();

            SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));
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
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        UpdateVisualComponents();
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;

        if (_text is null || _background is null)
            CreateVisualComponents();

        if (
            float.IsNaN(constraint.X)
            || constraint.X < 0f
            || float.IsNaN(constraint.Y)
            || constraint.Y < 0f
        )
            throw new ArgumentOutOfRangeException(nameof(constraint));

        var contentConstraint = new Vector2D<float>(
            MathF.Max(0f, constraint.X - Margins.Left - Margins.Right),
            MathF.Max(0f, constraint.Y - Margins.Top - Margins.Bottom)
        );
        contentConstraint = new(
            MathF.Min(contentConstraint.X, Width ?? contentConstraint.X),
            MathF.Min(contentConstraint.Y, Height ?? contentConstraint.Y)
        );
        var textConstraint = new Vector2D<float>(
            MathF.Max(0f, contentConstraint.X - Padding.Left - Padding.Right),
            MathF.Max(0f, contentConstraint.Y - Padding.Top - Padding.Bottom)
        );
        var labelSize = _text?.Measure(textConstraint) ?? Vector2D<float>.Zero;
        return new Vector2D<float>(
                Width is null
                    ? MathF.Min(labelSize.X + Padding.Left + Padding.Right, contentConstraint.X)
                    : contentConstraint.X,
                Height is null
                    ? MathF.Min(labelSize.Y + Padding.Top + Padding.Bottom, contentConstraint.Y)
                    : contentConstraint.Y
            )
            + Margins;
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        if (!IsEffectivelyVisible)
        {
            RemoveVisualComponents();
            SetBounds(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            return;
        }

        if (_text is null || _background is null)
            CreateVisualComponents();
        if (_text is null || _background is null)
            return;

        var availableBounds = bounds - Margins;
        var contentSize = new Vector2D<float>(
            MathF.Min(availableBounds.Size.X, Width ?? availableBounds.Size.X),
            MathF.Min(availableBounds.Size.Y, Height ?? availableBounds.Size.Y)
        );
        var contentBounds = GetAlignedContentBounds(bounds, contentSize);
        SetBounds(contentBounds);
        _text.Text = Label;
        _text.Wrap = false;
        _text.MaximumLines = 1;
        _text.Destination = new Rectangle<float>(
            contentBounds.Origin.X + Padding.Left,
            contentBounds.Origin.Y + Padding.Top,
            MathF.Max(0f, contentBounds.Size.X - Padding.Left - Padding.Right),
            MathF.Max(0f, contentBounds.Size.Y - Padding.Top - Padding.Bottom)
        );
        _text.Alignment = new Vector2D<float>(0.5f, 0.5f);
        _background.Destination = contentBounds;
    }

}
