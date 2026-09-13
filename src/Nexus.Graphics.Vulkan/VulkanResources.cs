namespace Nexus.Graphics.Vulkan;

public static class VulkanResources
{
    private static readonly string _assemblyName = typeof(VulkanResources).Assembly.GetName().Name!;

    public const string SolidColorMeshVert =
        "Nexus.Graphics.Vulkan.Resources.Shaders.SolidColorMesh.vert.spv";

    public const string SolidColorMeshFrag =
        "Nexus.Graphics.Vulkan.Resources.Shaders.SolidColorMesh.frag.spv";

    public static readonly ShaderDefinition[] ShaderDefinitions =
    [
        new(
            "SolidColorMesh",
            new ShaderSource.Embedded(_assemblyName, SolidColorMeshVert),
            new ShaderSource.Embedded(_assemblyName, SolidColorMeshFrag)
        ),
    ];
}
