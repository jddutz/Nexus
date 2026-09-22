namespace Nexus.Graphics.Vulkan.Commands;

public sealed class EndRenderingCommand : IVulkanCommand
{
    public EndRenderingCommand(uint renderPass)
    {
        Id = Guid.NewGuid();
        RenderPass = renderPass;
    }

    public Guid Id { get; }

    public bool IsSticky => true;

    public RenderPhase Phase => RenderPhase.AfterRenderPass;

    public uint RenderPass { get; }

    public PipelineId? PipelineId => null;

    public IDrawable? Drawable => null;

    public int RenderPriority => int.MinValue;

    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        vk.CmdEndRendering(commandBuffer);
    }
}
