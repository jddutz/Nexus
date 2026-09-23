namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Binds the vertex buffer used by a drawable.
/// </summary>
public sealed class BindVertexBufferCommand : IVulkanCommand
{
    /// <summary>
    /// Creates a vertex-buffer binding command.
    /// </summary>
    /// <param name="renderPassMask">The render-pass mask in which the buffer is used.</param>
    /// <param name="pipelineId">The pipeline identity used for batch ordering.</param>
    /// <param name="drawable">The drawable that owns this command.</param>
    /// <param name="binding">The vertex-input binding slot.</param>
    /// <param name="buffer">The Vulkan vertex buffer to bind.</param>
    public BindVertexBufferCommand(
        uint renderPassMask,
        PipelineId pipelineId,
        IDrawable drawable,
        uint binding,
        VkBuffer buffer
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);

        Id = Guid.NewGuid();
        RenderPassMask = renderPassMask;
        PipelineId = pipelineId;
        Drawable = drawable;
        Binding = binding;
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
    public int RenderPriority => 1;

    /// <summary>
    /// Gets the vertex-input binding slot.
    /// </summary>
    public uint Binding { get; }

    /// <summary>
    /// Gets the Vulkan vertex buffer to bind.
    /// </summary>
    public VkBuffer Buffer { get; }

    /// <inheritdoc />
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var buffers = Buffer;
        var offsets = 0UL;
        vk.CmdBindVertexBuffers(commandBuffer, Binding, 1, &buffers, &offsets);
    }
}
