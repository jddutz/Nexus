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
    private TextRenderer? _textComponent;

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
        if (_textComponent is not null)
            _textComponent.MaximumLines = MaximumLines;
    }

    /// <summary>Updates the component source after the authored text changes.</summary>
    /// <param name="previousValue">The previous source text.</param>
    protected virtual partial void AfterTextChanges(string previousValue)
    {
        if (_textComponent is not null)
            _textComponent.Text = Text;
    }

    /// <summary>Updates the text drawable after the render-layer mask changes.</summary>
    /// <param name="previousValue">The previous render-layer mask.</param>
    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_textComponent is not null)
            _textComponent.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Updates the text component after the glyph color changes.</summary>
    /// <param name="previousValue">The previous glyph color.</param>
    protected virtual partial void AfterColorChanges(Color previousValue)
    {
        if (_textComponent is not null)
            _textComponent.Color = Color;
    }

    /// <summary>Updates the graphics component when the text style changes.</summary>
    /// <param name="previousValue">The previous text style.</param>
    protected virtual partial void AfterStyleChanges(ITextStyle? previousValue)
    {
        if (_textComponent is not null)
            _textComponent.TextStyle = Style;
    }

    /// <summary>Initializes an empty text element.</summary>
    public TextElement()
    {
        if (IsEffectivelyVisible)
            EnsureVisualComponent();
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
        ulong renderLayerMask = RenderLayers.All
    )
        : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        if (maximumLines is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumLines));

        Text = text;
        Style = style;
        MaximumLines = maximumLines;
        RenderLayerMask = renderLayerMask;
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible || Style is null)
            return Vector2D<float>.Zero;

        EnsureVisualComponent();
        var contentConstraint = new Vector2D<float>(
            MathF.Max(0f, constraint.X - Margins.Left - Margins.Right),
            MathF.Max(0f, constraint.Y - Margins.Top - Margins.Bottom)
        );
        var measuredSize =
            _textComponent?.Measure(
                new(
                    MathF.Min(contentConstraint.X, Width ?? contentConstraint.X),
                    MathF.Min(contentConstraint.Y, Height ?? contentConstraint.Y)
                )
            )
            ?? Vector2D<float>.Zero;
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
            RemoveVisualComponent();
            base.Arrange(bounds);
            return;
        }

        EnsureVisualComponent();
        if (_textComponent is null)
            return;

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

        foreach (var child in Children)
            if (child is IElement element)
                element.Arrange(Bounds);
    }

    /// <summary>Creates a text component when measurement or arrangement requires one.</summary>
    private void EnsureVisualComponent()
    {
        if (_textComponent is not null)
            return;

        var textComponent = new TextRenderer
        {
            TextStyle = Style,
            Color = Color,
            RenderLayerMask = RenderLayerMask,
            Text = Text,
            MaximumLines = MaximumLines,
            Wrap = true,
        };
        _textComponent = textComponent;
        AddComponent(textComponent);
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
        if (!IsEffectivelyVisible)
        {
            RemoveVisualComponent();
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
