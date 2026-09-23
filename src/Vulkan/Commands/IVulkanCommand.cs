namespace Nexus.Graphics.Vulkan.Commands;

public interface IVulkanCommand
{
    Guid Id { get; }

    bool IsSticky { get; }

    uint RenderPassMask { get; }

    PipelineId? PipelineId { get; }

    IDrawable? Drawable { get; }

    int RenderPriority { get; }

    void Record(Vk vk, CommandBuffer commandBuffer);
}
