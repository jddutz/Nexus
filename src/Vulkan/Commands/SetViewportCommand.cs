namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Sets the viewport used by subsequent rendering commands.
/// </summary>
public sealed class SetViewportCommand(VkViewport viewport) : IVulkanCommand
{
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
