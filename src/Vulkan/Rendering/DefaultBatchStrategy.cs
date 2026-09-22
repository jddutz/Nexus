namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Orders Vulkan commands by render pass and pipeline to minimize
/// state changes while preserving render-pass boundaries.
/// </summary>
public sealed class DefaultBatchStrategy : IBatchStrategy
{
    /// <summary>
    /// Compares Vulkan commands by render pass and pipeline identifier.
    /// </summary>
    /// <param name="x">The first command to compare.</param>
    /// <param name="y">The second command to compare.</param>
    /// <returns>A value indicating the relative sort order of the commands.</returns>
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

        if (x.PipelineId is null)
        {
            if (y.PipelineId is not null)
                return -1;
        }
        else
        {
            if (y.PipelineId is null)
                return 1;

            // First .Value to remove the nullable wrapper,
            // second to expose the underlying ulong
            result = x.PipelineId.Value.Value.CompareTo(y.PipelineId.Value.Value);
            if (result != 0)
                return result;
        }

        result = x.RenderPriority.CompareTo(y.RenderPriority);
        if (result != 0)
            return result;

        return x.Id.CompareTo(y.Id);
    }
}
