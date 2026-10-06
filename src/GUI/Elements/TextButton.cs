namespace Nexus.GUI.Elements;

using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;

/// <summary>
/// Represents a text button with an instance-owned nine-patch background and text component.
/// </summary>
public partial class TextButton : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];
    private NinePatchRenderer? _background;
    private TextRenderer? _text;

    /// <summary>Gets or sets the optional texture used by the button background.</summary>
    [Observable(PublicSetter = true)]
    private ITexture? _texture;

    /// <summary>Gets or sets the optional style used by the button label.</summary>
    [Observable(PublicSetter = true)]
    private ITextStyle? _style;

    /// <summary>Gets or sets the source texture border widths.</summary>
    [Observable(PublicSetter = true)]
    private Vector4D<float> _sourceBorders = new(12f, 12f, 12f, 12f);

    /// <summary>Gets or sets the background texture sampling behavior.</summary>
    [Observable(PublicSetter = true)]
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.PixelPerfect;

    /// <summary>Gets or sets the render-layer mask shared by the button visuals.</summary>
    [Observable(PublicSetter = true)]
    private ulong _renderLayerMask = RenderLayers.All;

    [Observable(PublicSetter = true)]
    private string _label = string.Empty;

    /// <summary>Gets or sets the color applied to the button label.</summary>
    [Observable(PublicSetter = true)]
    private Color _textColor = Colors.White;

    [Observable(PublicSetter = true)]
    private Margins _padding = new(16f, 10f);

    /// <summary>Gets or sets the action invoked for a click, receiving this button instance.</summary>
    [Observable(PublicSetter = true)]
    private Action<TextButton>? _action;

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
        {
            _text.Text = Label;
            UpdateRendererVisibility();
        }
    }

    /// <summary>Updates the text component after the label color changes.</summary>
    /// <param name="previousValue">The previous label color.</param>
    protected virtual partial void AfterTextColorChanges(Color previousValue)
    {
        if (_text is not null)
            _text.Color = TextColor;
    }

    /// <summary>Updates the label renderer when its optional style changes.</summary>
    /// <param name="previousValue">The previous style.</param>
    protected virtual partial void AfterStyleChanges(ITextStyle? previousValue)
    {
        if (_text is not null)
        {
            _text.TextStyle = Style;
            UpdateRendererVisibility();
        }
    }

    /// <summary>Updates the background renderer when its optional texture changes.</summary>
    /// <param name="previousValue">The previous texture.</param>
    protected virtual partial void AfterTextureChanges(ITexture? previousValue)
    {
        if (_background is not null)
        {
            _background.Texture = Texture;
            UpdateRendererVisibility();
        }
    }

    /// <summary>Updates the background renderer when its source borders change.</summary>
    /// <param name="previousValue">The previous source borders.</param>
    protected virtual partial void AfterSourceBordersChanges(Vector4D<float> previousValue)
    {
        if (_background is not null)
            _background.SourceBorders = SourceBorders;
    }

    /// <summary>Updates the background renderer when its sampling behavior changes.</summary>
    /// <param name="previousValue">The previous sampling behavior.</param>
    protected virtual partial void AfterSamplingBehaviorChanges(ISamplingBehavior previousValue)
    {
        if (_background is not null)
            _background.SamplingBehavior = SamplingBehavior;
    }

    /// <summary>Rejects a null sampling behavior.</summary>
    /// <param name="value">The proposed sampling behavior.</param>
    private void BeforeSamplingBehaviorChanges(ISamplingBehavior value) =>
        ArgumentNullException.ThrowIfNull(value);

    /// <summary>Initializes a text button with empty, assignable visual resources.</summary>
    public TextButton()
    {
        SetCanFocus(true);
        InputMap.OnMouseButtonReleased(MouseButtonEnum.Left).Invoke(InvokeAction);
        CreateVisualComponents();
    }

    /// <summary>
    /// Initializes a text button with its initial visual resources and layout settings.
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
        ITexture texture,
        float horizontalPadding = 16f,
        float verticalPadding = 10f,
        ulong renderLayerMask = RenderLayers.All,
        Vector4D<float>? sourceBorders = null,
        ISamplingBehavior? samplingBehavior = null
    )
        : this()
    {
        ArgumentNullException.ThrowIfNull(textStyle);
        ArgumentNullException.ThrowIfNull(texture);

        Style = textStyle;
        Texture = texture;
        Padding = new(horizontalPadding, verticalPadding);
        RenderLayerMask = renderLayerMask;
        SourceBorders = sourceBorders ?? new Vector4D<float>(12f, 12f, 12f, 12f);
        SamplingBehavior = samplingBehavior ?? SamplingBehaviors.PixelPerfect;
    }

    /// <summary>Invokes the action assigned to this button, if any.</summary>
    private void InvokeAction() => Action?.Invoke(this);

    /// <summary>Creates and attaches the button's owned renderer components.</summary>
    private void CreateVisualComponents()
    {
        var background = new NinePatchRenderer
        {
            IsVisible = false,
            Texture = Texture,
            RenderLayerMask = RenderLayerMask,
            SamplingBehavior = SamplingBehavior,
            SourceBorders = SourceBorders,
        };
        var text = new TextRenderer
        {
            IsVisible = false,
            TextStyle = Style,
            Color = TextColor,
            RenderLayerMask = RenderLayerMask,
            Text = Label,
            Wrap = false,
            MaximumLines = 1,
        };
        _background = background;
        _text = text;
        AddComponent(background);
        AddComponent(text);
    }

    /// <summary>Synchronizes renderer visibility with effective visibility and drawable geometry.</summary>
    private void UpdateVisualComponents()
    {
        if (!IsEffectivelyVisible)
            SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));

        UpdateRendererVisibility();
    }

    /// <summary>Shows each button renderer only when its drawable geometry is renderable.</summary>
    private void UpdateRendererVisibility()
    {
        if (_background is null || _text is null)
            return;

        var canRender = IsEffectivelyVisible && HasRenderableGeometry(Bounds);
        _background.IsVisible = canRender && Texture is not null && _background.Drawables.Count > 0;
        _text.IsVisible =
            canRender
            && HasRenderableGeometry((Rectangle<float>)_text.Destination)
            && _text.Drawables.Count > 0;
    }

    /// <summary>Checks whether a rectangle has finite, positive dimensions.</summary>
    /// <param name="bounds">The rectangle to validate.</param>
    /// <returns>True when the rectangle can be rendered.</returns>
    private static bool HasRenderableGeometry(Rectangle<float> bounds) =>
        float.IsFinite(bounds.Origin.X)
        && float.IsFinite(bounds.Origin.Y)
        && float.IsFinite(bounds.Size.X)
        && float.IsFinite(bounds.Size.Y)
        && bounds.Size.X > 0f
        && bounds.Size.Y > 0f;

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
        var labelSize = Style is null ? Vector2D<float>.Zero : _text!.Measure(textConstraint);
        return new Vector2D<float>(
                Width is null
                    ? MathF.Min(labelSize.X + Padding.Left + Padding.Right, contentConstraint.X)
                    : contentConstraint.X,
                Height is null
                    ? MathF.Min(labelSize.Y + Padding.Top + Padding.Bottom, contentConstraint.Y)
                    : contentConstraint.Y
            ) + Margins;
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        if (!IsEffectivelyVisible)
        {
            SetBounds(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            UpdateRendererVisibility();
            return;
        }

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
        UpdateRendererVisibility();
    }
}
