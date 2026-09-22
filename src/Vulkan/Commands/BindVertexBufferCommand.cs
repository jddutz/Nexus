namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Binds the vertex buffer used by a drawable.
/// </summary>
public sealed class BindVertexBufferCommand : IVulkanCommand
{
    /// <summary>
    /// Creates a vertex-buffer binding command.
    /// </summary>
    /// <param name="renderPass">The render-pass mask in which the buffer is used.</param>
    /// <param name="pipelineId">The pipeline identity used for batch ordering.</param>
    /// <param name="drawable">The drawable that owns this command.</param>
    /// <param name="buffer">The Vulkan vertex buffer to bind.</param>
    public BindVertexBufferCommand(
        uint renderPass,
        PipelineId pipelineId,
        IDrawable drawable,
        VkBuffer buffer
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);

        Id = Guid.NewGuid();
        RenderPass = renderPass;
        PipelineId = pipelineId;
        Drawable = drawable;
        Buffer = buffer;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public bool IsSticky => true;

    /// <inheritdoc />
    public RenderPhase Phase => RenderPhase.RenderPass;

    /// <inheritdoc />
    public uint RenderPass { get; }

    /// <inheritdoc />
    public PipelineId? PipelineId { get; }

    /// <inheritdoc />
    public IDrawable Drawable { get; }

    /// <inheritdoc />
    public int RenderPriority => 1;

    /// <summary>
    /// Gets the Vulkan vertex buffer to bind.
    /// </summary>
    public VkBuffer Buffer { get; }

    /// <inheritdoc />
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var buffers = Buffer;
        var offsets = 0UL;
        vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &buffers, &offsets);
    }
}
