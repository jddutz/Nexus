namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Orders Vulkan commands into a valid command-buffer sequence while
/// minimizing pipeline state changes within each render pass.
/// </summary>
public sealed class DefaultBatchStrategy : IBatchStrategy
{
    public int Compare(IVulkanCommand? x, IVulkanCommand? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x is null)
            return -1;

        if (y is null)
            return 1;

        int result;

        if (x.PipelineId is null)
        {
            if (y.PipelineId is not null)
                return -1;
        }
        else
        {
            if (y.PipelineId is null)
                return 1;

            result = x.PipelineId.Value.Value.CompareTo(y.PipelineId.Value.Value);
            if (result != 0)
                return result;
        }

        if (x.Drawable is null)
        {
            if (y.Drawable is not null)
                return -1;
        }
        else
        {
            if (y.Drawable is null)
                return 1;

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
