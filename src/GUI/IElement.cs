namespace Nexus.GUI;

public interface IElement
{
    float? Height { get; set; }
    float? Width { get; set; }
    Rectangle<float> Bounds { get; set; }

    Vector2D<float> Measure(Vector2D<float> constraint);
    void Arrange(Rectangle<float> bounds);
}
