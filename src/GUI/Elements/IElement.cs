namespace Nexus.GUI;

/// <summary>
/// Defines the dimensions, hit-test bounds, and layout protocol of a GUI element.
/// </summary>
public interface IElement
{
    /// <summary>Gets or sets the explicit height requested for the element.</summary>
    float? Height { get; set; }

    /// <summary>Gets or sets the explicit width requested for the element.</summary>
    float? Width { get; set; }

    /// <summary>Gets or sets the element bounds used for hit testing.</summary>
    Rectangle<float> Bounds { get; set; }

    /// <summary>Measures the element within the specified size constraint.</summary>
    /// <param name="constraint">The available size constraint.</param>
    /// <returns>The measured size.</returns>
    Vector2D<float> Measure(Vector2D<float> constraint);

    /// <summary>Arranges the element within the specified bounds.</summary>
    /// <param name="bounds">The bounds assigned to the element.</param>
    void Arrange(Rectangle<float> bounds);
}
