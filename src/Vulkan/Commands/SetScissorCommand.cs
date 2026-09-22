namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Sets the scissor rectangle used by subsequent rendering commands.
/// </summary>
public sealed class SetScissorCommand(Rect2D scissor) : IVulkanCommand
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
    /// Gets the scissor rectangle to set.
    /// </summary>
    public Rect2D Scissor { get; } = scissor;

    /// <inheritdoc />
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var scissor = Scissor;

        vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
    }

    public bool IsRecorded { get; set; } = false;
}
