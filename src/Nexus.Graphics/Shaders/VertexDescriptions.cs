namespace Nexus.Graphics.Shaders;

public static class VertexDescriptions
{
    public static VertexDescription UniformColorVertex { get; } =
        new(
            stride: 16,
            attributes:
            [
                new VertexAttributeDescription(
                    semantic: VertexSemanticEnum.Position,
                    format: VertexFormatEnum.Float3,
                    location: 0,
                    offset: 0
                ),
            ]
        );
}
