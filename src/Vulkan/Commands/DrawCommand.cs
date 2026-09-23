namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Draws the vertices for a drawable.
/// </summary>
public sealed class DrawCommand : IVulkanCommand
{
    /// <summary>
    /// Creates a draw command.
    /// </summary>
    /// <param name="renderPassMask">The render-pass mask in which the draw is used.</param>
    /// <param name="pipelineId">The pipeline identity used for batch ordering.</param>
    /// <param name="drawable">The drawable that owns this command.</param>
    /// <param name="vertexCount">The number of vertices to draw.</param>
    /// <param name="instanceCount">The number of instances to draw.</param>
    /// <param name="firstVertex">The index of the first vertex.</param>
    /// <param name="firstInstance">The instance ID of the first instance.</param>
    public DrawCommand(
        uint renderPassMask,
        PipelineId pipelineId,
        IDrawable drawable,
        uint vertexCount,
        uint instanceCount = 1,
        uint firstVertex = 0,
        uint firstInstance = 0
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (vertexCount == 0)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        if (instanceCount == 0)
            throw new ArgumentOutOfRangeException(nameof(instanceCount));

        Id = Guid.NewGuid();
        RenderPassMask = renderPassMask;
        PipelineId = pipelineId;
        Drawable = drawable;
        VertexCount = vertexCount;
        InstanceCount = instanceCount;
        FirstVertex = firstVertex;
        FirstInstance = firstInstance;
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
    public int RenderPriority => int.MaxValue;

    /// <summary>
    /// Gets the number of vertices to draw.
    /// </summary>
    public uint VertexCount { get; }

    /// <summary>
    /// Gets the number of instances to draw.
    /// </summary>
    public uint InstanceCount { get; }

    /// <summary>
    /// Gets the index of the first vertex.
    /// </summary>
    public uint FirstVertex { get; }

    /// <summary>
    /// Gets the instance ID of the first instance.
    /// </summary>
    public uint FirstInstance { get; }

    /// <inheritdoc />
    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        vk.CmdDraw(commandBuffer, VertexCount, InstanceCount, FirstVertex, FirstInstance);
    }
}
