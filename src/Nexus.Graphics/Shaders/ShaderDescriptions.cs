namespace Nexus.Graphics.Shaders;

public static class ShaderDescriptions
{
    public static ShaderDescription UniformColorVertexShader { get; } =
        new(
            BuiltInResource.UniformColorVertexShader.Name,
            "uniform_color.vert.spv",
            ShaderStageEnum.Vertex,
            PrimitiveTopologyEnum.TriangleList,
            VertexDescriptions.UniformColorVertex
        );

    public static ShaderDescription UniformColorFragmentShader { get; } =
        new(
            BuiltInResource.UniformColorFragmentShader.Name,
            "uniform_color.frag.spv",
            ShaderStageEnum.Fragment,
            PrimitiveTopologyEnum.TriangleList,
            VertexDescriptions.UniformColorVertex
        );

    public static readonly ShaderDescription[] All =
    [
        UniformColorVertexShader,
        UniformColorFragmentShader,
    ];
}
