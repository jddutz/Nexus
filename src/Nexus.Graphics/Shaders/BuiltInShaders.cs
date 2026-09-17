namespace Nexus.Graphics.Shaders;

public static class BuiltInShaders
{
    public static Shader UniformColorVertexShader { get; } =
        new(
            nameof(UniformColorVertexShader),
            "uniform_color.vert",
            ShaderStageEnum.Vertex,
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.UniformColor
        );

    public static Shader UniformColorFragmentShader { get; } =
        new(
            nameof(UniformColorFragmentShader),
            "uniform_color.frag",
            ShaderStageEnum.Fragment,
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.UniformColor
        );

    public static Shader TexturedQuadVertexShader { get; } =
        new(
            nameof(TexturedQuadVertexShader),
            "textured_quad.vert",
            ShaderStageEnum.Vertex,
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.TexturedQuad
        );

    public static Shader TexturedQuadFragmentShader { get; } =
        new(
            nameof(TexturedQuadFragmentShader),
            "textured_quad.frag",
            ShaderStageEnum.Fragment,
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.TexturedQuad
        );

    public static readonly Shader[] All =
    [
        UniformColorVertexShader,
        UniformColorFragmentShader,
        TexturedQuadVertexShader,
        TexturedQuadFragmentShader,
    ];
}
