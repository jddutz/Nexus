namespace Nexus.Graphics.Shaders;

/// <summary>Provides the shader contracts bundled with the graphics library.</summary>
public static class BuiltInShaders
{
    /// <summary>Gets the vertex shader contract for uniform-color geometry.</summary>
    public static VertexShader UniformColorVertexShader { get; } =
        new(
            nameof(UniformColorVertexShader),
            "uniform_color.vert",
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.UniformColor,
            [new(InputSemantics.View, 64)],
            [new(InputSemantics.Transform, 64), new(InputSemantics.Color, 16)]
        );

    /// <summary>Gets the fragment shader contract for uniform-color geometry.</summary>
    public static FragmentShader UniformColorFragmentShader { get; } =
        new(
            nameof(UniformColorFragmentShader),
            "uniform_color.frag",
            BuiltInVertexFormats.UniformColor,
            ColorFormatEnum.RGBA8UNorm,
            []
        );

    /// <summary>Gets the vertex shader contract for textured quads.</summary>
    public static VertexShader TexturedQuadVertexShader { get; } =
        new(
            nameof(TexturedQuadVertexShader),
            "textured_quad.vert",
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.TexturedQuad,
            [new(InputSemantics.View, 64)],
            [
                new(InputSemantics.Transform, 64),
                new(InputSemantics.TextureRegion, 16),
                new(InputSemantics.Color, 16),
            ]
        );

    /// <summary>Gets the fragment shader contract for textured quads.</summary>
    public static FragmentShader TexturedQuadFragmentShader { get; } =
        new(
            nameof(TexturedQuadFragmentShader),
            "textured_quad.frag",
            BuiltInVertexFormats.TexturedQuad,
            ColorFormatEnum.RGBA8UNorm,
            []
        );

    /// <summary>Gets the vertex shader contract for MSDF text glyphs.</summary>
    public static VertexShader MsdfTextVertexShader { get; } =
        new(
            nameof(MsdfTextVertexShader),
            "msdf_text.vert",
            PrimitiveTopologyEnum.TriangleStrip,
            BuiltInVertexFormats.TexturedQuad,
            [new(InputSemantics.View, 64)],
            [
                new(InputSemantics.Transform, 64),
                new(InputSemantics.TextureRegion, 16),
                new(InputSemantics.Color, 16),
                new(InputSemantics.MsdfDistanceRange, 4),
            ]
        );

    /// <summary>Gets the fragment shader contract for MSDF text glyphs.</summary>
    public static FragmentShader MsdfTextFragmentShader { get; } =
        new(
            nameof(MsdfTextFragmentShader),
            "msdf_text.frag",
            BuiltInVertexFormats.TexturedQuad,
            ColorFormatEnum.RGBA8UNorm,
            []
        );

    /// <summary>Gets all built-in shader contracts.</summary>
    public static readonly IShaderContract[] All =
    [
        UniformColorVertexShader,
        UniformColorFragmentShader,
        TexturedQuadVertexShader,
        TexturedQuadFragmentShader,
        MsdfTextVertexShader,
        MsdfTextFragmentShader,
    ];
}
