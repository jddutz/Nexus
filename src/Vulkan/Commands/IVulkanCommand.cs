namespace Nexus.Graphics.Vulkan.Commands;

public interface IVulkanCommand
{
    Guid Id { get; }

    bool IsSticky { get; }

    uint RenderPass { get; }

    PipelineId? PipelineId { get; }

    void Record(Vk vk, CommandBuffer commandBuffer);

    bool IsRecorded { get; set; }
}
