namespace Nexus.Graphics.Resources;

public class SolidColorBackgroundResource : IGraphicsResource
{
    public ResourceId Id { get; init; }

    public Vector4D<float> BackgroundColor { get; init; }

    public SolidColorBackgroundResource(Vector4D<float> color)
    {
        BackgroundColor = color;

        Id = new IdentityHashBuilder(nameof(SolidColorBackgroundResource))
            .Add(BackgroundColor.X)
            .Add(BackgroundColor.Y)
            .Add(BackgroundColor.Z)
            .Add(BackgroundColor.W)
            .Compute();
    }
}
