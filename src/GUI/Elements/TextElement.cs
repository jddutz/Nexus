namespace Nexus.GUI.Elements;

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
    private ulong _renderLayerMask = ulong.MaxValue;
    private TextComponent? _textComponent;
    private Rectangle<float>? _layoutBounds;

    /// <summary>Gets or sets the font and visual style used by the text element.</summary>
    [Observable(PublicSetter = true)]
    private ITextStyle? _style;

    [Observable]
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;

    [Observable]
    private AlignVertical _verticalAlignment = AlignVertical.Center;

    private void BeforeHorizontalAlignmentChanges(AlignHorizontal value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void BeforeVerticalAlignmentChanges(AlignVertical value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    protected virtual partial void AfterHorizontalAlignmentChanges(AlignHorizontal previousValue) =>
        ReapplyLayout();

    protected virtual partial void AfterVerticalAlignmentChanges(AlignVertical previousValue) =>
        ReapplyLayout();

    /// <summary>Updates the component's line limit after it changes.</summary>
    /// <param name="previousValue">The previous line limit.</param>
    protected virtual partial void AfterMaximumLinesChanges(int? previousValue)
    {
        if (_textComponent is not null)
            _textComponent.MaximumLines = MaximumLines;
        ReapplyLayout();
    }

    /// <summary>Updates the component source after the authored text changes.</summary>
    /// <param name="previousValue">The previous source text.</param>
    protected virtual partial void AfterTextChanges(string previousValue)
    {
        if (_textComponent is not null)
            _textComponent.Text = Text;
        ReapplyLayout();
    }

    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_textComponent is not null)
            _textComponent.RenderLayerMask = RenderLayerMask;
    }

    /// <summary>Updates the graphics component when the text style changes.</summary>
    /// <param name="previousValue">The previous text style.</param>
    protected virtual partial void AfterStyleChanges(ITextStyle? previousValue)
    {
        if (_textComponent is not null)
            _textComponent.TextStyle = Style;
        ReapplyLayout();
    }

    /// <summary>Initializes an empty text element.</summary>
    public TextElement()
    {
        UpdateVisualComponent();
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
        ulong renderLayerMask = ulong.MaxValue
    ) : this()
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

        return _textComponent?.Measure(constraint) ?? Vector2D<float>.Zero;
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        _layoutBounds = bounds;
        if (!IsEffectivelyVisible || _textComponent is null)
            return;

        base.Arrange(bounds);
        _textComponent.Text = Text;
        _textComponent.MaximumLines = MaximumLines;
        _textComponent.Wrap = true;
        _textComponent.Destination = bounds;
        _textComponent.Alignment = new Vector2D<float>(
            GetHorizontalAlignment(),
            GetVerticalAlignment()
        );
    }

    /// <summary>Reapplies the last parent-assigned rectangle after layout-affecting state changes.</summary>
    private void ReapplyLayout()
    {
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Creates a fresh text component from the retained text configuration.</summary>
    private void CreateVisualComponent()
    {
        var textComponent = new TextComponent
        {
            TextStyle = Style,
            RenderLayerMask = RenderLayerMask,
            Text = Text,
            MaximumLines = MaximumLines,
            Wrap = true,
        };
        _textComponent = textComponent;
        AddComponent(textComponent);
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Gets the normalized horizontal alignment value for the text component.</summary>
    private float GetHorizontalAlignment() =>
        HorizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => 0.5f,
            AlignHorizontal.Right => 1f,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the normalized vertical alignment value for the text component.</summary>
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
        if (IsEffectivelyVisible)
        {
            if (_textComponent is null)
                CreateVisualComponent();
        }
        else if (_textComponent is not null)
        {
            RemoveVisualComponent();
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

    /// <summary>Updates text component ownership when an ancestor's visibility changes.</summary>
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
    protected override void AfterIsVisibleChanges() => UpdateVisualComponent();

}
