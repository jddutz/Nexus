namespace Nexus.Graphics.Vulkan.Commands;

public sealed class CopyBufferToImageCommand : IVulkanCommand
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool IsSticky => false;

    public uint RefCount => 1;

    public RenderPhase Phase => RenderPhase.AfterBeginCommandBuffer;

    public uint RenderPass => 0;

    public PipelineId? PipelineId => null;

    public IDrawable? Drawable => null;

    public int RenderPriority => 1;

    public bool IsRecorded { get; set; }

    private readonly VkBuffer _source;
    private readonly VkImage _destination;
    private readonly BufferImageCopy _region;

    public CopyBufferToImageCommand(VkBuffer source, VkImage destination, BufferImageCopy region)
    {
        _source = source;
        _destination = destination;
        _region = region;
    }

    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        vk.CmdCopyBufferToImage(
            commandBuffer,
            _source,
            _destination,
            ImageLayout.TransferDstOptimal,
            1,
            in _region
        );
    }
}
