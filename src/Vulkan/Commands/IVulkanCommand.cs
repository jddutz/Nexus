namespace Nexus.Graphics.Vulkan.Commands;

public interface IVulkanCommand
{
    Guid Id { get; }

    bool IsSticky { get; }

    RenderPhase Phase { get; }

    uint RenderPass { get; }

    PipelineId? PipelineId { get; }

    IDrawable? Drawable { get; }

    int RenderPriority { get; }

    void Record(Vk vk, CommandBuffer commandBuffer);
}
