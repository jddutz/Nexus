namespace Nexus.Graphics.Vulkan;

public class VulkanGraphicsSystem(IRenderer renderer, IGraphicsResourceManager resourceManager)
    : IGraphicsSystem
{
    private readonly IRenderer _renderer = renderer;
    private readonly IGraphicsResourceManager _resourceManager = resourceManager;

    public IGraphicsResourceManager ResourceManager => _resourceManager;

    public void Configure()
    {
        foreach (var resource in VulkanResources.ShaderDefinitions)
        {
            _resourceManager.Register(resource);
        }
    }

    public void Initialize() { }

    public void Render()
    {
        _renderer.Render();

        // TODO: handle rendering failure
    }
}
