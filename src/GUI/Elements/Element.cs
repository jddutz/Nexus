namespace Nexus.GUI.Elements;

/// <summary>
/// Represents a two-dimensional user-interface element with virtual layout behavior.
/// </summary>
public class Element : GameObject2D, IElement
{
    private float? _height;
    private float? _width;
    private Rectangle<float> _bounds;
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
            Bounds.Size.X > 0f
            && Bounds.Size.Y > 0f
            && position.X >= Bounds.Origin.X
            && position.X < Bounds.Max.X
            && position.Y >= Bounds.Origin.Y
            && position.Y < Bounds.Max.Y
        );

    /// <summary>Determines whether a window-coordinate position lies within the current bounds.</summary>
    /// <param name="position">The position to test.</param>
    /// <returns>True when the element has positive bounds containing the position.</returns>
    internal bool ContainsPointerPosition(Vector2D<float> position) =>
        Bounds.Size.X > 0f
        && Bounds.Size.Y > 0f
        && position.X >= Bounds.Origin.X
        && position.X < Bounds.Max.X
        && position.Y >= Bounds.Origin.Y
        && position.Y < Bounds.Max.Y;

    /// <summary>Notifies the element that a pointer entered its bounds.</summary>
    /// <param name="eventArgs">The pointer event data.</param>
    internal virtual void OnPointerEntered(PointerEventArgs eventArgs) { }

    /// <summary>Notifies the element that a pointer exited its bounds.</summary>
    /// <param name="eventArgs">The pointer event data.</param>
    internal virtual void OnPointerExited(PointerEventArgs eventArgs) { }

    /// <summary>Attempts to begin a pointer press owned by this element.</summary>
    /// <param name="eventArgs">The pointer event data.</param>
    /// <returns>True when this element accepts and owns the press.</returns>
    internal virtual bool TryPointerDown(PointerEventArgs eventArgs) => false;

    /// <summary>Notifies the element of movement for a pointer press it owns.</summary>
    /// <param name="eventArgs">The pointer event data.</param>
    internal virtual void OnPointerMoved(PointerEventArgs eventArgs) { }

    /// <summary>Notifies the element that its owned pointer press ended normally.</summary>
    /// <param name="eventArgs">The pointer event data.</param>
    internal virtual void OnPointerUp(PointerEventArgs eventArgs) { }

    /// <summary>Notifies the element that its owned pointer press was interrupted.</summary>
    /// <param name="eventArgs">The pointer event data.</param>
    internal virtual void OnPointerCanceled(PointerEventArgs eventArgs) { }

    /// <summary>Clears pointer state when input dispatch is interrupted or detached.</summary>
    internal virtual void CancelPointerInput() { }

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
