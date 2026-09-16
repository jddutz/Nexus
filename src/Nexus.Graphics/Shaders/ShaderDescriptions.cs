namespace Nexus.Graphics.Shaders;

public static class ShaderDescriptions
{
    public static ShaderDescription UniformColorVertexShader { get; } =
        new(
            "UniformColorVertexShader",
            "uniform_color.vert.spv",
            ShaderStageEnum.Vertex,
            PrimitiveTopologyEnum.TriangleStrip,
            VertexDescriptions.UniformColorVertex
        );

    public static ShaderDescription UniformColorFragmentShader { get; } =
        new(
            "UniformColorFragmentShader",
            "uniform_color.frag.spv",
            ShaderStageEnum.Fragment,
            PrimitiveTopologyEnum.TriangleStrip,
            VertexDescriptions.UniformColorVertex
        );

    public static ShaderDescription TexturedQuadVertexShader { get; } =
        new(
            "TexturedQuadVertexShader",
            "textured_quad.vert.spv",
            ShaderStageEnum.Vertex,
            PrimitiveTopologyEnum.TriangleStrip,
            VertexDescriptions.TexturedQuadVertex
        );

    public static ShaderDescription TexturedQuadFragmentShader { get; } =
        new(
            "TexturedQuadFragmentShader",
            "textured_quad.frag.spv",
            ShaderStageEnum.Fragment,
            PrimitiveTopologyEnum.TriangleStrip,
            VertexDescriptions.TexturedQuadVertex
        );

    public static readonly ShaderDescription[] All =
    [
        UniformColorVertexShader,
        UniformColorFragmentShader,
        TexturedQuadVertexShader,
        TexturedQuadFragmentShader,
    ];
}
