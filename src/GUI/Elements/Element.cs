namespace Nexus.GUI.Elements;

/// <summary>
/// Represents a two-dimensional user-interface element with virtual layout behavior.
/// </summary>
public partial class Element : GameObject, IElement
{
    [Observable]
    private float? _height = null;

    [Observable]
    private float? _width = null;

    [Observable(PublicSetter = true)]
    private Rectangle<float> _bounds = default;

    [Observable(PublicSetter = true)]
    private bool _isVisible = true;

    [Observable(PublicSetter = true)]
    private bool _isEnabled = true;

    [Observable(PublicSetter = true)]
    private bool _canFocus;

    [Observable(PublicSetter = true)]
    private bool _isFocused;
    private InputMap? _inputMap;

    /// <summary>
    /// Initializes an element with its owned components.
    /// </summary>
    /// <param name="components">The components owned by the element.</param>
    public Element(IEnumerable<Nexus.Core.IComponent>? components = null)
        : base(components ?? []) { }

    /// <summary>Occurs when this element receives focus.</summary>
    public event EventHandler? FocusGained;

    /// <summary>Occurs when this element loses focus.</summary>
    public event EventHandler? FocusLost;

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
        for (ISceneNode? current = gameObject; current is not null; current = current.Parent)
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
        for (ISceneNode? current = gameObject; current is not null; current = current.Parent)
        {
            if (current is IElement element && !element.IsEnabled)
                return false;
        }

        return true;
    }

    /// <summary>Rejects focus when this element is not focusable.</summary>
    /// <param name="value">Whether this element should be focused.</param>
    private bool ValidateIsFocused(bool value) => !value || CanFocus;

    /// <summary>Raises the focus lifecycle event after the observable focus state changes.</summary>
    /// <param name="previousValue">The focus state before the change.</param>
    protected virtual partial void AfterIsFocusedChanges(bool previousValue)
    {
        if (IsFocused)
            FocusGained?.Invoke(this, EventArgs.Empty);
        else
            FocusLost?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears hit-test bounds when this element becomes hidden.</summary>
    protected virtual partial void AfterIsVisibleChanges()
    {
        if (!IsEffectivelyVisible)
            SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));
    }

    /// <summary>Clears directly assigned hit bounds when the element is hidden.</summary>
    /// <param name="previousValue">The bounds before the change.</param>
    protected virtual partial void AfterBoundsChanges(Rectangle<float> previousValue)
    {
        if (!IsEffectivelyVisible && Bounds.Size != Vector2D<float>.Zero)
            SetBounds(new Rectangle<float>(Bounds.Origin, Vector2D<float>.Zero));
    }

    /// <summary>Requests a fresh measure-and-arrange pass from the GUI.</summary>
    protected void InvalidateLayout() => NotifyPropertyChanged(string.Empty);

    /// <summary>
    /// Measures the element within the specified constraint.
    /// </summary>
    /// <param name="constraint">The available size constraint.</param>
    /// <returns>The measured size.</returns>
    public virtual Vector2D<float> Measure(Vector2D<float> constraint) =>
        new(Width ?? constraint.X, Height ?? constraint.Y);

    /// <summary>
    /// Arranges the element within the specified allocation.
    /// </summary>
    /// <param name="bounds">The allocation assigned to the element.</param>
    public virtual void Arrange(Rectangle<float> bounds)
    {
        SetBounds(
            IsEffectivelyVisible
                ? bounds
                : new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero)
        );
    }
}
