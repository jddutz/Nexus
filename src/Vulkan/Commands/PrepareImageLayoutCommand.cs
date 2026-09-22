namespace Nexus.Graphics.Vulkan.Commands;

public sealed unsafe class PrepareImageLayoutCommand(VkImage image) : IVulkanCommand
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool IsSticky => false;

    public RenderPhase Phase => RenderPhase.AfterBeginCommandBuffer;

    public uint RenderPass => 0;

    public PipelineId? PipelineId => null;

    public IDrawable? Drawable => null;

    public int RenderPriority => 0;

    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,

            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.TransferDstOptimal,

            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,

            Image = image,

            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },

            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.TransferWriteBit,
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.TransferBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in barrier
        );
    }
}
