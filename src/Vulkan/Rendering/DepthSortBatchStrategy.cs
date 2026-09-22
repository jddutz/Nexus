namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Batch strategy intended for back-to-front rendering of transparent drawables.
/// </summary>
/// <remarks>
/// Depth sorting will be added when camera position, frustum, and view-projection
/// information are available to the rendering system.
/// </remarks>
public sealed class DepthSortBatchStrategy : IBatchStrategy
{
    public int Compare(IVulkanCommand? x, IVulkanCommand? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x is null)
            return -1;

        if (y is null)
            return 1;

        var result = x.RenderPass.CompareTo(y.RenderPass);
        if (result != 0)
            return result;

        result = x.RenderPriority.CompareTo(y.RenderPriority);
        if (result != 0)
            return result;

        // TODO: Sort drawable commands back-to-front when camera position,
        // frustum, and view-projection information are available.

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

        return x.Id.CompareTo(y.Id);
    }
}
