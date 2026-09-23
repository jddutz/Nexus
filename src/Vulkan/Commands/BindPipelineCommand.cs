namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Binds a graphics pipeline for a drawable.
/// </summary>
public sealed class BindPipelineCommand : IVulkanCommand
{
    /// <summary>
    /// Creates a pipeline-binding command.
    /// </summary>
    /// <param name="renderPass">The render-pass mask in which the pipeline is used.</param>
    /// <param name="pipelineId">The pipeline identity used for batch ordering.</param>
    /// <param name="drawable">The drawable that owns this command.</param>
    /// <param name="pipeline">The Vulkan pipeline to bind.</param>
    public BindPipelineCommand(
        uint renderPass,
        PipelineId pipelineId,
        IDrawable drawable,
        Pipeline pipeline
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);

        Id = Guid.NewGuid();
        RenderPassIndex = renderPass;
        PipelineId = pipelineId;
        Drawable = drawable;
        Pipeline = pipeline;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public bool IsSticky => true;

    /// <inheritdoc />
    public int Phase => RenderPasses.RenderPass;

    /// <inheritdoc />
    public uint RenderPassIndex { get; }

    /// <inheritdoc />
    public PipelineId? PipelineId { get; }

    /// <inheritdoc />
    public IDrawable Drawable { get; }

    /// <inheritdoc />
    public int RenderPriority => 0;

    /// <summary>
    /// Gets the Vulkan pipeline to bind.
    /// </summary>
    public Pipeline Pipeline { get; }

    /// <inheritdoc />
    public void Record(Vk vk, CommandBuffer commandBuffer) =>
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, Pipeline);
}
