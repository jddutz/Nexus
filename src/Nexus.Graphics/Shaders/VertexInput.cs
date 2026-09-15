namespace Nexus.Graphics.Shaders;

public sealed record VertexInput(
    uint Location,
    VertexSemanticEnum Semantic,
    VertexFormatEnum Format
);
