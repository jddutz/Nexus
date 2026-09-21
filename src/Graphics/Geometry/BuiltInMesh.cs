namespace Nexus.Graphics.Geometry;

public static class BuiltInMesh
{
    public static Mesh Empty => new(nameof(Empty), PrimitiveTopologyEnum.TriangleList, []);

    public static Mesh FullScreenTriangle =>
        new(
            nameof(FullScreenTriangle),
            PrimitiveTopologyEnum.TriangleList,
            [new(new(-1f, -1f, 0f)), new(new(3f, -1f, 0f)), new(new(-1f, 3f, 0f))]
        );

    public static Mesh UniformColorRectCentered =>
        new(
            nameof(UniformColorRectCentered),
            PrimitiveTopologyEnum.TriangleStrip,
            [
                new(new(-0.5f, -0.5f, 0f)),
                new(new(0.5f, -0.5f, 0f)),
                new(new(-0.5f, 0.5f, 0f)),
                new(new(0.5f, 0.5f, 0f)),
            ]
        );

    public static Mesh UniformColorRectOffset =>
        new(
            nameof(UniformColorRectOffset),
            PrimitiveTopologyEnum.TriangleStrip,
            [
                new(new(0.0f, 0.0f, 0f)),
                new(new(1.0f, 0.0f, 0f)),
                new(new(0.0f, 1.0f, 0f)),
                new(new(1.0f, 1.0f, 0f)),
            ]
        );

    public static Mesh TexturedQuadCentered =>
        new(
            nameof(TexturedQuadCentered),
            PrimitiveTopologyEnum.TriangleStrip,
            [
                new(new(-0.5f, -0.5f, 0f), texCoord: new(0f, 0f)),
                new(new(0.5f, -0.5f, 0f), texCoord: new(1f, 0f)),
                new(new(-0.5f, 0.5f, 0f), texCoord: new(0f, 1f)),
                new(new(0.5f, 0.5f, 0f), texCoord: new(1f, 1f)),
            ]
        );

    public static Mesh TexturedQuadOffset =>
        new(
            nameof(TexturedQuadOffset),
            PrimitiveTopologyEnum.TriangleStrip,
            [
                new(new(0.0f, 0.0f, 0f), texCoord: new(0f, 0f)),
                new(new(1.0f, 0.0f, 0f), texCoord: new(1f, 0f)),
                new(new(0.0f, 1.0f, 0f), texCoord: new(0f, 1f)),
                new(new(1.0f, 1.0f, 0f), texCoord: new(1f, 1f)),
            ]
        );
}
