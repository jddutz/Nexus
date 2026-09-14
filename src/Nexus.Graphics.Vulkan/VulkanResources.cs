namespace Nexus.Graphics.Vulkan;

public static class VulkanResources
{
    public static readonly ShaderDefinition[] ShaderDefinitions =
    [
        new(
            "UniformColorVert",
            new ShaderSource("uniform_color.vert.spv"),
            ShaderStageFlags.VertexBit,
            new ShaderContract()
        ),
        new(
            "UniformColorFrag",
            new ShaderSource("uniform_color.vert.spv"),
            ShaderStageFlags.FragmentBit,
            new ShaderContract()
        ),
    ];
}
