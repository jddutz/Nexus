namespace Nexus.GUI.Elements;

/// <summary>
/// Represents a two-dimensional user-interface element with virtual layout behavior.
/// </summary>
public class Element : GameObject2D, IElement
{
    private float? _height;
    private float? _width;
    private Rectangle<float> _bounds;
    private bool _isVisible = true;
    private bool _isEnabled = true;
    private bool _canFocus;
    private bool _isFocused;
    private InputMap? _inputMap;

    /// <summary>
    /// Initializes an element with optional explicit dimensions and owned components.
    /// </summary>
    /// <param name="width">The initial width of the element.</param>
    /// <param name="height">The initial height of the element.</param>
    /// <param name="components">The components owned by the element.</param>
    public Element(
        float? width = null,
        float? height = null,
        IEnumerable<Nexus.Core.IComponent>? components = null
    )
        : base(components ?? [])
    {
        _width = width;
        _height = height;
    }

    /// <summary>Gets or sets whether this element is visible.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Gets or sets whether this element is enabled for interaction.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>Gets or sets whether this element is eligible to receive focus.</summary>
    public bool CanFocus
    {
        get => _canFocus;
        set => SetProperty(ref _canFocus, value);
    }

    /// <summary>Gets whether this element currently has focus.</summary>
    public bool IsFocused => _isFocused;

    /// <summary>Occurs when this element receives focus.</summary>
    public event EventHandler? FocusGained;

    /// <summary>Occurs when this element loses focus.</summary>
    public event EventHandler? FocusLost;

    /// <summary>
    /// Gets or sets the element's height.
    /// </summary>
    public float? Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    /// <summary>
    /// Gets or sets the element's width.
    /// </summary>
    public float? Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    /// <summary>
    /// Gets or sets the element's arranged bounds.
    /// </summary>
    public Rectangle<float> Bounds
    {
        get => _bounds;
        set => SetProperty(ref _bounds, value);
    }

    /// <summary>Gets the input bindings associated with this element.</summary>
    public InputMap InputMap =>
        _inputMap ??= new InputMap(hitTest: position =>
            IsEffectivelyVisible
            && IsEffectivelyEnabled
            && Bounds.Size.X > 0f
            && Bounds.Size.Y > 0f
            && position.X >= Bounds.Origin.X
            && position.X < Bounds.Max.X
            && position.Y >= Bounds.Origin.Y
            && position.Y < Bounds.Max.Y
        );

    /// <summary>Gets whether this element and all ancestor elements are visible.</summary>
    internal bool IsEffectivelyVisible => AreAncestorsVisible(this);

    /// <summary>Gets whether this element and all ancestor elements are enabled.</summary>
    internal bool IsEffectivelyEnabled => AreAncestorsEnabled(this);

    /// <summary>Checks local visibility on this element and its ancestors.</summary>
    /// <param name="gameObject">The element whose ancestor chain is checked.</param>
    /// <returns>True when no element in the ancestor chain is hidden.</returns>
    private static bool AreAncestorsVisible(IGameObject gameObject)
    {
        for (var current = gameObject; current is not null; current = current.Parent)
        {
            if (current is IElement element && !element.IsVisible)
                return false;
        }

        return true;
    }

    /// <summary>Checks local enabled state on this element and its ancestors.</summary>
    /// <param name="gameObject">The element whose ancestor chain is checked.</param>
    /// <returns>True when no element in the ancestor chain is disabled.</returns>
    private static bool AreAncestorsEnabled(IGameObject gameObject)
    {
        for (var current = gameObject; current is not null; current = current.Parent)
        {
            if (current is IElement element && !element.IsEnabled)
                return false;
        }

        return true;
    }

    /// <summary>Updates focus state on behalf of the GUI focus manager.</summary>
    /// <param name="isFocused">Whether this element should be focused.</param>
    internal void SetFocused(bool isFocused)
    {
        if (isFocused && !CanFocus)
            throw new InvalidOperationException(
                "An element must be focusable before it can receive focus."
            );

        if (!SetProperty(ref _isFocused, isFocused))
            return;

        if (isFocused)
            FocusGained?.Invoke(this, EventArgs.Empty);
        else
            FocusLost?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Measures the element within the specified constraint.
    /// </summary>
    /// <param name="constraint">The available size constraint.</param>
    /// <returns>The measured size.</returns>
    public virtual Vector2D<float> Measure(Vector2D<float> constraint) =>
        new(Width ?? constraint.X, Height ?? constraint.Y);

    /// <summary>
    /// Arranges the element within the specified bounds.
    /// </summary>
    /// <param name="bounds">The bounds assigned to the element.</param>
    public virtual void Arrange(Rectangle<float> bounds) => Bounds = bounds;
}
