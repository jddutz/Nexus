namespace Nexus.Graphics.Vulkan.Commands;

public sealed class DrawCommand : IVulkanCommand
{
    public DrawCommand(
        uint renderPass,
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
        RenderPassIndex = renderPass;
        PipelineId = pipelineId;
        Drawable = drawable;
        VertexCount = vertexCount;
        InstanceCount = instanceCount;
        FirstVertex = firstVertex;
        FirstInstance = firstInstance;
    }

    public Guid Id { get; }

    public bool IsSticky => true;

    public int Phase => RenderPasses.RenderPass;

    public uint RenderPassIndex { get; }

    public PipelineId? PipelineId { get; }

    public IDrawable Drawable { get; }

    public int RenderPriority => int.MaxValue;

    public uint VertexCount { get; }

    public uint InstanceCount { get; }

    public uint FirstVertex { get; }

    public uint FirstInstance { get; }

    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        vk.CmdDraw(commandBuffer, VertexCount, InstanceCount, FirstVertex, FirstInstance);
    }
}
