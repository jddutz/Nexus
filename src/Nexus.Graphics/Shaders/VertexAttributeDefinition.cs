namespace Nexus.Graphics.Shaders;

public readonly record struct VertexAttributeDefinition(
    VertexSemanticEnum Semantic,
    VertexFormatEnum Format,
    uint Location
);
