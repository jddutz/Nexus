namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Sets the viewport used by subsequent rendering commands.
/// </summary>
public sealed class SetViewportCommand(VkViewport viewport) : IVulkanCommand
{
    /// <summary>
    /// Gets the render-pass ordering value for this command.
    /// </summary>
    public uint RenderPass => 0;

    /// <summary>
    /// Gets the pipeline ordering value for this command.
    /// </summary>
    public PipelineId? PipelineId => null;

    public Guid Id { get; } = Guid.NewGuid();

    public bool IsSticky => false;

    /// <summary>
    /// Gets the viewport to set.
    /// </summary>
    public VkViewport Viewport { get; } = viewport;

    /// <inheritdoc />
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var viewport = Viewport;

        vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
    }

    public bool IsRecorded { get; set; } = false;
}
