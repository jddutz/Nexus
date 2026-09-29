namespace Nexus.GUI;

/// <summary>
/// Defines the dimensions, hit-test bounds, and layout protocol of a GUI element.
/// </summary>
public interface IElement
{
    /// <summary>Gets or sets whether this element is visible.</summary>
    /// <remarks>An element is effectively visible only when it and all ancestor elements are visible.</remarks>
    bool IsVisible { get; set; }

    /// <summary>Gets or sets whether this element is enabled for interaction.</summary>
    /// <remarks>An element is effectively enabled only when it and all ancestor elements are enabled.</remarks>
    bool IsEnabled { get; set; }

    /// <summary>Gets or sets whether the element is eligible to receive focus.</summary>
    bool CanFocus { get; set; }

    /// <summary>Gets whether the element currently has focus.</summary>
    bool IsFocused { get; }

    /// <summary>Occurs when the element receives focus.</summary>
    event EventHandler? FocusGained;

    /// <summary>Occurs when the element loses focus.</summary>
    event EventHandler? FocusLost;

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
