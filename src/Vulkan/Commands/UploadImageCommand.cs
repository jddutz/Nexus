namespace Nexus.Graphics.Vulkan.Commands;

public sealed unsafe class UploadImageCommand(
    VkBuffer source,
    VkImage destination,
    BufferImageCopy region
) : IVulkanCommand
{
    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public bool IsSticky => false;

    /// <inheritdoc />
    public uint RenderPassMask => RenderPasses.Start;

    /// <inheritdoc />
    public PipelineId? PipelineId => null;

    /// <inheritdoc />
    public IDrawable? Drawable => null;

    /// <inheritdoc />
    public int RenderPriority => 0;

    /// <inheritdoc />
    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.TransferDstOptimal,

            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,

            Image = destination,

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

        vk.CmdCopyBufferToImage(
            commandBuffer,
            source,
            destination,
            ImageLayout.TransferDstOptimal,
            1,
            in region
        );

        barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,

            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,

            Image = destination,

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
