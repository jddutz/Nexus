namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Binds descriptor sets used by a drawable.
/// </summary>
public sealed class BindDescriptorSetsCommand : IVulkanCommand
{
    /// <summary>
    /// Creates a descriptor-set binding command.
    /// </summary>
    /// <param name="renderPass">The render-pass mask in which the sets are used.</param>
    /// <param name="pipelineId">The pipeline identity used for batch ordering.</param>
    /// <param name="drawable">The drawable that owns this command.</param>
    /// <param name="pipelineLayout">The layout used to validate the descriptor sets.</param>
    /// <param name="descriptorSets">The descriptor sets to bind, starting at set zero.</param>
    public BindDescriptorSetsCommand(
        uint renderPass,
        PipelineId pipelineId,
        IDrawable drawable,
        PipelineLayout pipelineLayout,
        IReadOnlyList<DescriptorSet> descriptorSets
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);
        ArgumentNullException.ThrowIfNull(descriptorSets);

        if (descriptorSets.Count == 0)
            throw new ArgumentException(
                "At least one descriptor set is required.",
                nameof(descriptorSets)
            );

        Id = Guid.NewGuid();
        RenderPassIndex = renderPass;
        PipelineId = pipelineId;
        Drawable = drawable;
        PipelineLayout = pipelineLayout;
        DescriptorSets = [.. descriptorSets];
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public bool IsSticky => true;

    public uint RenderPassMask => RenderPasses.Before;

    /// <inheritdoc />
    public PipelineId? PipelineId { get; }

    /// <inheritdoc />
    public IDrawable Drawable { get; }

    /// <inheritdoc />
    public int RenderPriority => 2;

    /// <summary>
    /// Gets the pipeline layout used for binding.
    /// </summary>
    public PipelineLayout PipelineLayout { get; }

    /// <summary>
    /// Gets the descriptor sets to bind.
    /// </summary>
    public IReadOnlyList<DescriptorSet> DescriptorSets { get; }

    /// <inheritdoc />
    public unsafe void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var descriptorSets = DescriptorSets.ToArray();
        fixed (DescriptorSet* descriptorSetPointer = descriptorSets)
        {
            vk.CmdBindDescriptorSets(
                commandBuffer,
                PipelineBindPoint.Graphics,
                PipelineLayout,
                0,
                (uint)descriptorSets.Length,
                descriptorSetPointer,
                0,
                null
            );
        }
    }
}
