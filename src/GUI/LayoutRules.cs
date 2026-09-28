using System.Threading.Channels;

namespace Nexus.GUI;

/// <summary>
/// Defines measurement and arrangement callbacks used by GUI elements.
/// </summary>
/// <param name="element">The element being measured.</param>
/// <param name="availableSize">The size available for measurement.</param>
public delegate Vector2D<float> MeasurementRule(Element element, Vector2D<float> availableSize);

/// <summary>
/// Defines an arrangement callback for a GUI element.
/// </summary>
/// <param name="element">The element being arranged.</param>
/// <param name="availableRect">The bounds available for arrangement.</param>
public delegate void ArrangementRule(Element element, Rectangle<float> availableRect);

/// <summary>
/// Provides default measurement rules for GUI elements.
/// </summary>
public static class MeasurementRules
{
    /// <summary>
    /// Measures the element using its explicit dimensions or the available size.
    /// </summary>
    /// <param name="element">The element to measure.</param>
    /// <param name="available">The available size.</param>
    /// <returns>The measured size.</returns>
    public static Vector2D<float> Default(Element element, Vector2D<float> available)
    {
        return new(element.Width ?? available.X, element.Height ?? available.Y);
    }
}

/// <summary>
/// Provides default arrangement rules for GUI elements.
/// </summary>
public static class ArrangementRules
{
    /// <summary>
    /// Assigns bounds to an active element and arranges its active element children.
    /// </summary>
    /// <param name="element">The element to arrange.</param>
    /// <param name="bounds">The bounds assigned to the element.</param>
    public static void Default(Element element, Rectangle<float> bounds)
    {
        element.Bounds = bounds;

        foreach (var child in element.Children.OfType<Element>().Where(child => child.IsActive))
        {
            child.Arrange(bounds);
        }
    }
}
