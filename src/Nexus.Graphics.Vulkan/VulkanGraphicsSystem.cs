namespace Nexus.Graphics.Vulkan;

public class VulkanGraphicsSystem(IRenderer renderer) : IGraphicsSystem
{
    private readonly IRenderer _renderer = renderer;

    public IGraphicsResource[] ResourceCatalog => [.. VulkanResources.ShaderDefinitions];

    public void Configure() { }

    public void Initialize() { }

    public void Render()
    {
        _renderer.Render();

        // TODO: handle rendering failure
    }
}
