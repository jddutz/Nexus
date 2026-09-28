using Nexus.Core;
using Nexus.Input;

namespace Nexus.GUI;

/// <summary>
/// Represents a two-dimensional user-interface element with configurable layout rules.
/// </summary>
/// <param name="width">The initial width of the element.</param>
/// <param name="height">The initial height of the element.</param>
/// <param name="measure">The rule used to measure the element.</param>
/// <param name="arrange">The rule used to arrange the element.</param>
/// <param name="components">The components owned by the element.</param>
public class Element(
    float? width = null,
    float? height = null,
    MeasurementRule? measure = null,
    ArrangementRule? arrange = null,
    IEnumerable<IComponent>? components = null
) : GameObject2D(components ?? []), IElement
{
    private float? _height = height;
    private float? _width = width;
    private Rectangle<float> _bounds;
    private InputMap? _inputMap;

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

    private MeasurementRule _measurementRule = measure ?? MeasurementRules.Default;

    /// <summary>
    /// Measures the element within the specified constraint.
    /// </summary>
    /// <param name="constraint">The available size constraint.</param>
    /// <returns>The measured size.</returns>
    public Vector2D<float> Measure(Vector2D<float> constraint) =>
        _measurementRule(this, constraint);

    private ArrangementRule _arrangementRule = arrange ?? ArrangementRules.Default;

    /// <summary>
    /// Arranges the element within the specified bounds.
    /// </summary>
    /// <param name="bounds">The bounds assigned to the element.</param>
    public void Arrange(Rectangle<float> bounds) => _arrangementRule(this, bounds);
}
