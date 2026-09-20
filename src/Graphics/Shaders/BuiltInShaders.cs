namespace Nexus.Graphics.Shaders;

public static class BuiltInShaders
{
    public static VertexShader UniformColorVertexShader { get; } =
        new(
            nameof(UniformColorVertexShader),
            "uniform_color.vert",
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.UniformColor,
            [],
            []
        );

    public static FragmentShader UniformColorFragmentShader { get; } =
        new(
            nameof(UniformColorFragmentShader),
            "uniform_color.frag",
            BuiltInVertexFormats.UniformColor,
            ColorFormatEnum.RGBA8UNorm,
            [],
            []
        );

    public static VertexShader TexturedQuadVertexShader { get; } =
        new(
            nameof(TexturedQuadVertexShader),
            "textured_quad.vert",
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.TexturedQuad,
            [],
            []
        );

    public static FragmentShader TexturedQuadFragmentShader { get; } =
        new(
            nameof(TexturedQuadFragmentShader),
            "textured_quad.frag",
            BuiltInVertexFormats.TexturedQuad,
            ColorFormatEnum.RGBA8UNorm,
            [],
            []
        );

    public static readonly Shader[] All =
    [
        UniformColorVertexShader,
        UniformColorFragmentShader,
        TexturedQuadVertexShader,
        TexturedQuadFragmentShader,
    ];
}
