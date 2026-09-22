public sealed class BeginRenderPassCommand(
    RenderPass renderPass,
    Framebuffer framebuffer,
    Rect2D renderArea,
    ClearValue clearValue
) : IVulkanCommand
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool IsSticky => true;

    public bool IsRecorded { get; set; }

    public uint RenderPass => throw new NotImplementedException();

    public PipelineId? PipelineId => throw new NotImplementedException();

    /// <inheritdoc />
    public IDrawable? Drawable => null;

    /// <inheritdoc />
    public int RenderPriority { get; set; } = int.MinValue;

    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var clear = clearValue;

        var info = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = renderPass,
            Framebuffer = framebuffer,
            RenderArea = renderArea,
            ClearValueCount = 1,
            PClearValues = &clear,
        };

        vk.CmdBeginRenderPass(commandBuffer, &info, SubpassContents.Inline);
    }
}
