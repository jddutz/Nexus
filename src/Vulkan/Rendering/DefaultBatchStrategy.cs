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

        var result = x.RenderPassMask.CompareTo(y.RenderPassMask);
        if (result != 0)
            return result;

        if (x.PipelineId is null && y.PipelineId is not null)
            return -1;

        if (x.PipelineId is not null && y.PipelineId is null)
            return 1;

        if (x.Drawable is null && y.Drawable is not null)
            return -1;

        if (x.Drawable is not null && y.Drawable is null)
            return 1;

        if (x.PipelineId is not null && y.PipelineId is not null)
        {
            result = x.PipelineId.Value.Value.CompareTo(y.PipelineId.Value.Value);
            if (result != 0)
                return result;
        }

        if (x.Drawable is not null && y.Drawable is not null)
        {
            result = x.Drawable.Id.Value.CompareTo(y.Drawable.Id.Value);
            if (result != 0)
                return result;
        }

        result = x.RenderPriority.CompareTo(y.RenderPriority);
        if (result != 0)
            return result;

        return x.Id.CompareTo(y.Id);
    }
}
