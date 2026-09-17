namespace Nexus.Graphics.Geometry;

public static class BuiltInVertexFormats
{
    public static VertexFormat UniformColor { get; } = new([VertexSemanticEnum.Position]);

    public static VertexFormat TexturedQuad { get; } =
        new(
            [VertexSemanticEnum.Position, VertexSemanticEnum.TexCoord],
            positionFormat: VectorFormatEnum.Float2D
        );

    public static readonly VertexFormat[] All = [UniformColor, TexturedQuad];
}
