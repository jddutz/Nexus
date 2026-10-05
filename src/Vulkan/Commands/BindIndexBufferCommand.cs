namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>Binds the 32-bit index buffer used by an indexed drawable.</summary>
public sealed class BindIndexBufferCommand : IVulkanCommand
{
    /// <summary>Creates an index-buffer binding command.</summary>
    /// <param name="renderPassMask">The render-pass mask in which the buffer is used.</param>
    /// <param name="pipelineId">The pipeline identity used for batch ordering.</param>
    /// <param name="drawable">The drawable that owns this command.</param>
    /// <param name="buffer">The Vulkan index buffer to bind.</param>
    public BindIndexBufferCommand(
        uint renderPassMask,
        PipelineId pipelineId,
        IDrawable drawable,
        VkBuffer buffer
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);
        Id = Guid.NewGuid();
        RenderPassMask = renderPassMask;
        PipelineId = pipelineId;
        Drawable = drawable;
        Buffer = buffer;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public bool IsSticky => true;

    /// <inheritdoc />
    public uint RenderPassMask { get; }

    /// <inheritdoc />
    public PipelineId? PipelineId { get; }

    /// <inheritdoc />
    public IDrawable Drawable { get; }

    /// <inheritdoc />
    public long RenderPriority => 2;

    /// <summary>Gets the Vulkan index buffer.</summary>
    public VkBuffer Buffer { get; }

    /// <inheritdoc />
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer) =>
        vk.CmdBindIndexBuffer(commandBuffer, Buffer, 0, IndexType.Uint32);
}
