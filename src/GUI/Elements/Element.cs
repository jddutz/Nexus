namespace Nexus.GUI.Elements;

/// <summary>
/// Represents a two-dimensional user-interface element with virtual layout behavior.
/// </summary>
public partial class Element : GameObject, IElement
{
    private const int MinimumSortOrder = -32768;
    private const int MaximumSortOrder = 32768;

    [Observable]
    private float? _height = null;

    [Observable]
    private float? _width = null;

    [Observable(PublicSetter = true)]
    private Rectangle<float> _bounds = default;

    [Observable]
    private Margins _margins;

    /// <summary>Validates and assigns margins before the observable wrapper raises notifications.</summary>
    /// <param name="value">The margins to assign.</param>
    protected virtual void SetMargins(Margins value)
    {
        if (
            !float.IsFinite(value.Left)
            || !float.IsFinite(value.Right)
            || !float.IsFinite(value.Top)
            || !float.IsFinite(value.Bottom)
            || value.Left < 0f
            || value.Right < 0f
            || value.Top < 0f
            || value.Bottom < 0f
        )
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Margin values must be finite and non-negative."
            );

        _margins = value;
    }

    [Observable]
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;

    [Observable]
    private AlignVertical _verticalAlignment = AlignVertical.Center;

    [Observable(PublicSetter = true)]
    private bool _isVisible = true;

    [Observable(PublicSetter = true)]
    private bool _isEnabled = true;

    [Observable(PublicSetter = true)]
    private int _sortOrder;

    /// <summary>Clamps the sort order to the supported rendering range.</summary>
    /// <param name="value">The requested sort order.</param>
    protected virtual void SetSortOrder(int value) =>
        _sortOrder = Math.Clamp(value, MinimumSortOrder, MaximumSortOrder);

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
        : base(components ?? [])
    {
        ComponentAdded += OnComponentAdded;
        ApplySortOrder();
    }

    /// <summary>Applies a changed sort order to all owned graphics components.</summary>
    /// <param name="previousValue">The previous sort order.</param>
    protected virtual partial void AfterSortOrderChanges(int previousValue) => ApplySortOrder();

    /// <summary>Applies the element sort order to one newly added component.</summary>
    /// <param name="component">The component added to this element.</param>
    private void OnComponentAdded(Nexus.Core.IComponent component)
    {
        if (component is IGraphicsComponent graphicsComponent)
            graphicsComponent.DrawOrder = SortOrder;
    }

    /// <summary>Applies the current sort order to every owned graphics component.</summary>
    private void ApplySortOrder()
    {
        foreach (var graphicsComponent in Components.OfType<IGraphicsComponent>())
            graphicsComponent.DrawOrder = SortOrder;
    }

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

    /// <summary>Arranges each direct child within the supplied bounds.</summary>
    /// <param name="bounds">The bounds assigned to each child.</param>
    protected void ArrangeChildren(Rectangle<float> bounds)
    {
        foreach (var child in Children)
            if (child is IElement element)
                element.Arrange(bounds);
    }

    /// <summary>Aligns a desired content size within the allocation after applying margins.</summary>
    /// <param name="bounds">The outer allocation.</param>
    /// <param name="contentSize">The desired size of the rendered content.</param>
    /// <returns>The aligned bounds, constrained to the available content area.</returns>
    protected Rectangle<float> GetAlignedContentBounds(
        Rectangle<float> bounds,
        Vector2D<float> contentSize
    )
    {
        var availableBounds = bounds - Margins;
        var width = MathF.Min(MathF.Max(0f, contentSize.X), availableBounds.Size.X);
        var height = MathF.Min(MathF.Max(0f, contentSize.Y), availableBounds.Size.Y);
        var horizontalOffset = HorizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => (availableBounds.Size.X - width) / 2f,
            AlignHorizontal.Right => availableBounds.Size.X - width,
            _ => throw new InvalidOperationException("The horizontal alignment is invalid."),
        };
        var verticalOffset = VerticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => (availableBounds.Size.Y - height) / 2f,
            AlignVertical.Bottom => availableBounds.Size.Y - height,
            _ => throw new InvalidOperationException("The vertical alignment is invalid."),
        };
        return new Rectangle<float>(
            availableBounds.Origin.X + horizontalOffset,
            availableBounds.Origin.Y + verticalOffset,
            width,
            height
        );
    }

    /// <summary>
    /// Measures the element within the specified constraint.
    /// </summary>
    /// <param name="constraint">The available size constraint.</param>
    /// <returns>The measured size.</returns>
    public virtual Vector2D<float> Measure(Vector2D<float> constraint)
    {
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;

        var contentConstraint = constraint - Margins;

        return new Vector2D<float>(
            MathF.Min(Width ?? contentConstraint.X, contentConstraint.X),
            MathF.Min(Height ?? contentConstraint.Y, contentConstraint.Y)
        );
    }

    /// <summary>
    /// Arranges the element within the specified allocation.
    /// </summary>
    /// <param name="bounds">The allocation assigned to the element.</param>
    public virtual void Arrange(Rectangle<float> bounds)
    {
        if (!IsEffectivelyVisible)
        {
            SetBounds(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            ArrangeChildren(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            return;
        }

        var contentRect = bounds - Margins;

        var aligned = GetAlignedContentBounds(
            bounds,
            new(
                MathF.Min(Width ?? contentRect.Size.X, contentRect.Size.X),
                MathF.Min(Height ?? contentRect.Size.Y, contentRect.Size.Y)
            )
        );

        SetBounds(aligned);
        ArrangeChildren(aligned);
    }
}
