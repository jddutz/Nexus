namespace Nexus.Graphics.Vulkan.Rendering;

public enum RenderPhase
{
    AfterBeginCommandBuffer,
    BeforeRenderPass,
    RenderPass,
    AfterRenderPass,
    BeforeEndCommandBuffer,
}
