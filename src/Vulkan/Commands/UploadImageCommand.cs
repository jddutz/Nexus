namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>Uploads staging-buffer contents into a Vulkan image.</summary>
/// <param name="source">The staging buffer containing pixel data.</param>
/// <param name="destination">The destination image.</param>
/// <param name="region">The image region to populate.</param>
/// <param name="oldLayout">The image layout before the upload begins.</param>
public sealed unsafe class UploadImageCommand(
    VkBuffer source,
    VkImage destination,
    BufferImageCopy region,
    ImageLayout oldLayout = ImageLayout.Undefined
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
            OldLayout = oldLayout,
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

            SrcAccessMask = oldLayout == ImageLayout.Undefined ? 0 : AccessFlags.ShaderReadBit,
            DstAccessMask = AccessFlags.TransferWriteBit,
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            oldLayout == ImageLayout.Undefined
                ? PipelineStageFlags.TopOfPipeBit
                : PipelineStageFlags.FragmentShaderBit,
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
