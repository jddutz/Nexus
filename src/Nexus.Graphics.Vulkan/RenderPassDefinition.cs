namespace Nexus.Graphics.Vulkan;

public class RenderPassDefinition
{
    public uint RenderPass { get; set; } = RenderPasses.Main;
    public bool ShouldRender { get; set; } = true;

    public ClearValue[] ClearValues { get; set; } = [Colors.Magenta.ClearValue()];
}
