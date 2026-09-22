namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Orders Vulkan commands into a valid command-buffer sequence while
/// minimizing pipeline state changes within each render pass.
/// </summary>
public sealed class DefaultBatchStrategy : IBatchStrategy
{
    /// <inheritdoc />
    public int Compare(IVulkanCommand? x, IVulkanCommand? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x is null)
            return -1;

        if (y is null)
            return 1;

        var xGroup = GetGroup(x);
        var yGroup = GetGroup(y);

        var result = xGroup.CompareTo(yGroup);
        if (result != 0)
            return result;

        // Commands associated with rendering scopes are ordered
        // first by render pass, then by their position relative
        // to that pass.
        if (IsRenderPassPhase(x.Phase))
        {
            result = x.RenderPass.CompareTo(y.RenderPass);
            if (result != 0)
                return result;

            result = x.Phase.CompareTo(y.Phase);
            if (result != 0)
                return result;

            // Pipeline grouping is meaningful only while inside
            // the rendering scope.
            if (x.Phase == RenderPhase.RenderPass)
            {
                result = ComparePipeline(x.PipelineId, y.PipelineId);
                if (result != 0)
                    return result;

                result = CompareDrawable(x.Drawable, y.Drawable);
                if (result != 0)
                    return result;
            }
        }

        result = x.RenderPriority.CompareTo(y.RenderPriority);
        if (result != 0)
            return result;

        return x.Id.CompareTo(y.Id);
    }

    private static int GetGroup(IVulkanCommand command)
    {
        return command.Phase switch
        {
            RenderPhase.AfterBeginCommandBuffer => 0,

            RenderPhase.BeforeRenderPass or RenderPhase.RenderPass or RenderPhase.AfterRenderPass =>
                1,

            RenderPhase.BeforeEndCommandBuffer => 2,

            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.Phase,
                "Unsupported render phase."
            ),
        };
    }

    private static bool IsRenderPassPhase(RenderPhase phase)
    {
        return phase
            is RenderPhase.BeforeRenderPass
                or RenderPhase.RenderPass
                or RenderPhase.AfterRenderPass;
    }

    private static int ComparePipeline(PipelineId? x, PipelineId? y)
    {
        if (x is null)
            return y is null ? 0 : -1;

        if (y is null)
            return 1;

        return x.Value.Value.CompareTo(y.Value.Value);
    }

    private static int CompareDrawable(IDrawable? x, IDrawable? y)
    {
        if (x is null)
            return y is null ? 0 : -1;

        if (y is null)
            return 1;

        return x.Id.Value.CompareTo(y.Id.Value);
    }
}
