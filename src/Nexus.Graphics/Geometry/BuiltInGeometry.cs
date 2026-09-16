namespace Nexus.Graphics.Geometry;

public static class BuiltInGeometry
{
    public static IGeometry FullScreenTriangle =>
        new UniformColorVertexGeometry(
            nameof(FullScreenTriangle),
            [new(-1f, -1f, 0f), new(3f, -1f, 0f), new(-1f, 3f, 0f)]
        );

    public static IGeometry UniformColorRectCentered =>
        new UniformColorVertexGeometry(
            nameof(UniformColorRectCentered),
            [
                new(-0.5f, -0.5f, 0f),
                new(0.5f, -0.5f, 0f),
                new(-0.5f, 0.5f, 0f),
                new(0.5f, 0.5f, 0f),
            ],
            PrimitiveTopologyEnum.TriangleStrip
        );

    public static IGeometry UniformColorRectOffset =>
        new UniformColorVertexGeometry(
            nameof(UniformColorRectOffset),
            [new(0.0f, 0.0f, 0f), new(1.0f, 0.0f, 0f), new(0.0f, 1.0f, 0f), new(1.0f, 1.0f, 0f)],
            PrimitiveTopologyEnum.TriangleStrip
        );

    public static IGeometry TexturedQuadCentered =>
        new TexturedVertex2dGeometry(
            nameof(TexturedQuadCentered),
            [
                new(-0.5f, -0.5f, 0f, 0f),
                new(0.5f, -0.5f, 1f, 0f),
                new(-0.5f, 0.5f, 0f, 1f),
                new(0.5f, 0.5f, 1f, 1f),
            ],
            PrimitiveTopologyEnum.TriangleStrip
        );

    public static IGeometry TexturedQuadOffset =>
        new TexturedVertex2dGeometry(
            nameof(TexturedQuadOffset),
            [
                new(0.0f, 0.0f, 0f, 0f),
                new(1.0f, 0.0f, 1f, 0f),
                new(0.0f, 1.0f, 0f, 1f),
                new(1.0f, 1.0f, 1f, 1f),
            ],
            PrimitiveTopologyEnum.TriangleStrip
        );
}
