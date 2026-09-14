namespace Nexus.Graphics.Vulkan;

public static class VulkanResources
{
    public static readonly ShaderDefinition[] ShaderDefinitions =
    [
        new(
            "UniformColor",
            new ShaderSource("uniform_color.vert.spv"),
            new ShaderSource("uniform_color.frag.spv"),
            ShaderStageEnum.Vertex
        ),
    ];
}
