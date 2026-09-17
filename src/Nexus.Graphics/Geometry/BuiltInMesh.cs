namespace Nexus.Graphics.Geometry;

public static class BuiltInMesh
{
    public static Mesh FullScreenTriangle =>
        new UniformColorVertexGeometry(
            nameof(FullScreenTriangle),
            [new(-1f, -1f, 0f), new(3f, -1f, 0f), new(-1f, 3f, 0f)]
        );

    public static Mesh UniformColorRectCentered =>
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

    public static Mesh UniformColorRectOffset =>
        new UniformColorVertexGeometry(
            nameof(UniformColorRectOffset),
            [new(0.0f, 0.0f, 0f), new(1.0f, 0.0f, 0f), new(0.0f, 1.0f, 0f), new(1.0f, 1.0f, 0f)],
            PrimitiveTopologyEnum.TriangleStrip
        );

    public static Mesh TexturedQuadCentered =>
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

    public static Mesh TexturedQuadOffset =>
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
