namespace Nexus.Graphics.Vulkan;

public class GraphicsSystem(IRenderer renderer) : IGraphicsSystem
{
    private readonly IRenderer _renderer = renderer;

    public void Configure() { }

    public void Render()
    {
        _renderer.Render();

        // TODO: handle rendering failure
    }
}
