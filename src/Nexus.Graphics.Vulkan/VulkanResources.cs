namespace Nexus.Graphics.Vulkan;

public static class VulkanResources
{
    public static ShaderDefinition UniformColorVertShader { get; } =
        new(
            "UniformColorVert",
            new ShaderSource("uniform_color.vert.spv"),
            ShaderStageFlags.VertexBit,
            new ShaderContract()
        );

    public static ShaderDefinition UniformColorFragShader { get; } =
        new(
            "UniformColorFrag",
            new ShaderSource("uniform_color.vert.spv"),
            ShaderStageFlags.FragmentBit,
            new ShaderContract()
        );

    public static readonly ShaderDefinition[] ShaderDefinitions =
    [
        UniformColorVertShader,
        UniformColorFragShader,
    ];
}
