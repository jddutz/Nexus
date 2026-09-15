namespace Nexus.Graphics.Vulkan.Components;

public static class ResourceDefinitions
{
    public static ShaderDefinition UniformColorVertexShader { get; } =
        new(
            BuiltInResource.UniformColorVertexShader.Name,
            "uniform_color.vert.spv",
            ShaderStageFlags.VertexBit,
            new ShaderContract()
        );

    public static ShaderDefinition UniformColorFragmentShader { get; } =
        new(
            BuiltInResource.UniformColorFragmentShader.Name,
            "uniform_color.frag.spv",
            ShaderStageFlags.FragmentBit,
            new ShaderContract()
        );

    public static readonly ShaderDefinition[] ShaderDefinitions =
    [
        UniformColorVertexShader,
        UniformColorFragmentShader,
    ];
}
