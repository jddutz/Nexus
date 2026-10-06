namespace Nexus.GUI.Elements;

using Nexus.Graphics;
using Nexus.Graphics.Components;
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
    private ulong _renderLayerMask = RenderLayers.All;
    private readonly TextRenderer _textComponent;

    /// <summary>Gets or sets the color applied to the text glyphs.</summary>
    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;

    /// <summary>Gets or sets the font and visual style used by the text element.</summary>
    [Observable(PublicSetter = true)]
    private ITextStyle? _style;

    /// <summary>Updates the component's line limit after it changes.</summary>
    /// <param name="previousValue">The previous line limit.</param>
    protected virtual partial void AfterMaximumLinesChanges(int? previousValue)
    {
        _textComponent.MaximumLines = MaximumLines;
        UpdateRendererVisibility();
    }

    /// <summary>Updates the component source after the authored text changes.</summary>
    /// <param name="previousValue">The previous source text.</param>
    protected virtual partial void AfterTextChanges(string previousValue)
    {
        _textComponent.Text = Text;
        UpdateRendererVisibility();
    }

    /// <summary>Updates the text drawable after the render-layer mask changes.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        _textComponent.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Updates the text component after the glyph color changes.</summary>
    /// <param name="previousValue">The previous glyph color.</param>
    protected virtual partial void AfterColorChanges(Color previousValue)
    {
        _textComponent.Color = Color;
    }

    /// <summary>Updates the graphics component when the text style changes.</summary>
    /// <param name="previousValue">The previous text style.</param>
    protected virtual partial void AfterStyleChanges(ITextStyle? previousValue)
    {
        _textComponent.TextStyle = Style;
        UpdateRendererVisibility();
    }

    /// <summary>Initializes an empty text element.</summary>
    public TextElement()
    {
        _textComponent = new TextRenderer { IsVisible = false };
        AddComponent(_textComponent);
    }

    /// <summary>Initializes a text element with initial text and optional style data.</summary>
    /// <param name="text">The initial source text.</param>
    /// <param name="style">The optional text style.</param>
    public TextElement(string text, ITextStyle? style)
        : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Style = style;
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible || Style is null)
            return Vector2D<float>.Zero;

        var contentConstraint = new Vector2D<float>(
            MathF.Max(0f, constraint.X - Margins.Left - Margins.Right),
            MathF.Max(0f, constraint.Y - Margins.Top - Margins.Bottom)
        );
        var measuredSize = _textComponent.Measure(
            new(
                MathF.Min(contentConstraint.X, Width ?? contentConstraint.X),
                MathF.Min(contentConstraint.Y, Height ?? contentConstraint.Y)
            )
        );
        return new Vector2D<float>(
                Width is null ? measuredSize.X : MathF.Min(Width.Value, contentConstraint.X),
                Height is null ? measuredSize.Y : MathF.Min(Height.Value, contentConstraint.Y)
            ) + Margins;
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        if (!IsEffectivelyVisible)
        {
            base.Arrange(bounds);
            UpdateVisualComponent();
            return;
        }

        var contentBounds = bounds - Margins;
        _textComponent.Text = Text;
        _textComponent.MaximumLines = MaximumLines;
        _textComponent.Wrap = true;
        var contentSize = new Vector2D<float>(
            MathF.Min(contentBounds.Size.X, Width ?? contentBounds.Size.X),
            MathF.Min(contentBounds.Size.Y, Height ?? contentBounds.Size.Y)
        );
        _textComponent.Destination = GetAlignedContentBounds(bounds, contentSize);
        _textComponent.Alignment = new Vector2D<float>(
            GetHorizontalAlignment(),
            GetVerticalAlignment()
        );
        SetBounds(_textComponent.LayoutBounds);
        UpdateRendererVisibility();

        foreach (var child in Children)
            if (child is IElement element)
                element.Arrange(Bounds);
    }

    /// <summary>Gets the normalized horizontal alignment value for the text component.</summary>
    /// <returns>The normalized horizontal alignment.</returns>
    private float GetHorizontalAlignment() =>
        HorizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => 0.5f,
            AlignHorizontal.Right => 1f,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the normalized vertical alignment value for the text component.</summary>
    /// <returns>The normalized vertical alignment.</returns>
    private float GetVerticalAlignment() =>
        VerticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => 0.5f,
            AlignVertical.Bottom => 1f,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Synchronizes text renderer visibility with the element's renderable geometry.</summary>
    private void UpdateVisualComponent()
    {
        if (!IsEffectivelyVisible)
        {
            SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));
        }

        UpdateRendererVisibility();
    }

    /// <summary>Shows text only when it has visible glyphs inside valid element bounds.</summary>
    private void UpdateRendererVisibility() =>
        _textComponent!.IsVisible =
            IsEffectivelyVisible
            && HasRenderableGeometry(Bounds)
            && _textComponent.Drawables.Count > 0;

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

    /// <summary>Updates the text component when an ancestor's visibility changes.</summary>
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
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        UpdateVisualComponent();
    }
}
