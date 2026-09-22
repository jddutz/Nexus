namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Sets the scissor rectangle used by subsequent rendering commands.
/// </summary>
public sealed class SetScissorCommand(Rect2D scissor) : IVulkanCommand
{
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
