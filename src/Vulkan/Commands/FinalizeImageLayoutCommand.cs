namespace Nexus.Graphics.Vulkan.Commands;

public sealed unsafe class FinalizeImageLayoutCommand(VkImage image) : IVulkanCommand
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool IsSticky => false;

    public uint RefCount => 1;

    public RenderPhase Phase => RenderPhase.AfterBeginCommandBuffer;

    public uint RenderPass => 0;

    public PipelineId? PipelineId => null;

    public IDrawable? Drawable => null;

    public int RenderPriority => 2;

    public bool IsRecorded { get; set; }

    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,

            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,

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

            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.ShaderReadBit,
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.FragmentShaderBit,
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
