namespace Nexus.Graphics.Vulkan.Rendering;

public class RenderPassDefinition
{
    public uint RenderPass { get; set; }
    public bool ShouldRender { get; set; }

    public ClearValue[] ClearValues { get; set; } = [];
}
