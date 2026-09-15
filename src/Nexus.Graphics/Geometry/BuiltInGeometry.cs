namespace Nexus.Graphics.Geometry;

public static class BuiltInGeometry
{
    public static IGeometry FullScreenTriangle =>
        new UniformColorVertexGeometry(
            nameof(FullScreenTriangle),
            [new(-1f, -1f, 0f), new(3f, -1f, 0f), new(-1f, 3f, 0f)]
        );

    public static IGeometry RectCentered =>
        new UniformColorVertexGeometry(
            nameof(RectCentered),
            [
                new(-0.5f, -0.5f, 0f),
                new(0.5f, -0.5f, 0f),
                new(-0.5f, 0.5f, 0f),
                new(0.5f, 0.5f, 0f),
            ],
            PrimitiveTopologyEnum.TriangleStrip
        );

    public static IGeometry RectOffset =>
        new UniformColorVertexGeometry(
            nameof(RectOffset),
            [new(0.0f, 0.0f, 0f), new(1.0f, 0.0f, 0f), new(0.0f, 1.0f, 0f), new(1.0f, 1.0f, 0f)],
            PrimitiveTopologyEnum.TriangleStrip
        );
}
