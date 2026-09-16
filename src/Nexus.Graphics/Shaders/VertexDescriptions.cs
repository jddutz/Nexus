namespace Nexus.Graphics.Shaders;

/// <summary>
/// Provides vertex-input descriptions for built-in graphics pipelines.
/// </summary>
public static class VertexDescriptions
{
    /// <summary>
    /// Gets the layout of a uniform-color mesh vertex.
    /// </summary>
    public static VertexDescription UniformColorVertex { get; } =
        new(
            stride: 12,
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

    /// <summary>
    /// Gets the layout of a uniform-color mesh instance record.
    /// </summary>
    public static VertexDescription UniformColorInstance { get; } =
        new(
            stride: 80,
            attributes:
            [
                new VertexAttributeDescription(
                    semantic: VertexSemanticEnum.Transform,
                    format: VertexFormatEnum.Float4,
                    location: 1,
                    offset: 0
                ),
                new VertexAttributeDescription(
                    semantic: VertexSemanticEnum.Transform,
                    format: VertexFormatEnum.Float4,
                    location: 2,
                    offset: 16
                ),
                new VertexAttributeDescription(
                    semantic: VertexSemanticEnum.Transform,
                    format: VertexFormatEnum.Float4,
                    location: 3,
                    offset: 32
                ),
                new VertexAttributeDescription(
                    semantic: VertexSemanticEnum.Transform,
                    format: VertexFormatEnum.Float4,
                    location: 4,
                    offset: 48
                ),
                new VertexAttributeDescription(
                    semantic: VertexSemanticEnum.Color,
                    format: VertexFormatEnum.Float4,
                    location: 5,
                    offset: 64
                ),
            ]
        );
}
