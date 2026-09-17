namespace Nexus.Graphics.Geometry;

public static class BuiltInMesh
{
    public static Mesh FullScreenTriangle =>
        new Mesh(
            nameof(FullScreenTriangle),
            PrimitiveTopologyEnum.TriangleList,
            new MeshSource([new(-1f, -1f, 0f), new(3f, -1f, 0f), new(-1f, 3f, 0f)])
        );

    public static Mesh UniformColorRectCentered =>
        new Mesh(
            nameof(UniformColorRectCentered),
            PrimitiveTopologyEnum.TriangleStrip,
            new MeshSource([
                new(-0.5f, -0.5f, 0f),
                new(0.5f, -0.5f, 0f),
                new(-0.5f, 0.5f, 0f),
                new(0.5f, 0.5f, 0f),
            ])
        );

    public static Mesh UniformColorRectOffset =>
        new Mesh(
            nameof(UniformColorRectOffset),
            PrimitiveTopologyEnum.TriangleStrip,
            new MeshSource([
                new(0.0f, 0.0f, 0f),
                new(1.0f, 0.0f, 0f),
                new(0.0f, 1.0f, 0f),
                new(1.0f, 1.0f, 0f),
            ])
        );

    public static Mesh TexturedQuadCentered =>
        new Mesh(
            nameof(TexturedQuadCentered),
            PrimitiveTopologyEnum.TriangleStrip,
            new MeshSource([
                new(new(-0.5f, -0.5f, 0f), texCoord: new(0f, 0f)),
                new(new(0.5f, -0.5f, 0f), texCoord: new(1f, 0f)),
                new(new(-0.5f, 0.5f, 0f), texCoord: new(0f, 1f)),
                new(new(0.5f, 0.5f, 0f), texCoord: new(1f, 1f)),
            ])
        );

    public static Mesh TexturedQuadOffset =>
        new Mesh(
            nameof(TexturedQuadOffset),
            PrimitiveTopologyEnum.TriangleStrip,
            new MeshSource([
                new(new(0.0f, 0.0f, 0f), texCoord: new(0f, 0f)),
                new(new(1.0f, 0.0f, 0f), texCoord: new(1f, 0f)),
                new(new(0.0f, 1.0f, 0f), texCoord: new(0f, 1f)),
                new(new(1.0f, 1.0f, 0f), texCoord: new(1f, 1f)),
            ])
        );
}
