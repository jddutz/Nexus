namespace Nexus.Graphics.Vulkan;

public class RenderPassDefinition
{
    public uint RenderPass { get; set; }
    public bool ShouldRender { get; set; }

    public ClearValue[] ClearValues { get; set; } = [];
}
