namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>Updates a view-projection uniform buffer during command recording.</summary>
/// <param name="buffer">The uniform buffer to update.</param>
/// <param name="data">The aligned bytes written to the buffer.</param>
public sealed class UpdateUniformBufferCommand : IVulkanCommand
{
    private readonly VkBuffer _buffer;
    private readonly byte[] _data;

    /// <summary>Creates a transfer command for a uniform-buffer update.</summary>
    /// <param name="buffer">The uniform buffer to update.</param>
    /// <param name="data">The aligned bytes written to the buffer.</param>
    /// <exception cref="ArgumentException">The data size is invalid for <c>vkCmdUpdateBuffer</c>.</exception>
    public UpdateUniformBufferCommand(VkBuffer buffer, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data.Length > 65536 || data.Length % sizeof(uint) != 0)
            throw new ArgumentException(
                "Uniform update data must be nonempty, four-byte aligned, and at most 65536 bytes.",
                nameof(data)
            );

        _buffer = buffer;
        _data = data.ToArray();
    }

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
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var barrier = new BufferMemoryBarrier
        {
            SType = StructureType.BufferMemoryBarrier,
            SrcAccessMask = AccessFlags.UniformReadBit,
            DstAccessMask = AccessFlags.TransferWriteBit,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = _buffer,
            Offset = 0,
            Size = checked((ulong)_data.Length),
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.VertexShaderBit,
            PipelineStageFlags.TransferBit,
            0,
            0,
            null,
            1,
            &barrier,
            0,
            null
        );

        fixed (byte* data = _data)
            vk.CmdUpdateBuffer(commandBuffer, _buffer, 0, checked((ulong)_data.Length), data);

        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.UniformReadBit;
        vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.VertexShaderBit,
            0,
            0,
            null,
            1,
            &barrier,
            0,
            null
        );
    }
}
