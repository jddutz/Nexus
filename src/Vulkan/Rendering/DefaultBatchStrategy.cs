namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Orders Vulkan commands by render pass and pipeline to minimize
/// state changes while preserving render-pass boundaries.
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

        var comparison = x.RenderPass.CompareTo(y.RenderPass);
        if (comparison != 0)
            return comparison;

        comparison = ComparePipelineId(x.PipelineId, y.PipelineId);
        if (comparison != 0)
            return comparison;

        return x.Id.CompareTo(y.Id);
    }

    private static int ComparePipelineId(PipelineId? x, PipelineId? y)
    {
        if (x is null)
            return y is null ? 0 : -1;

        if (y is null)
            return 1;

        return ((ulong)x.Value).CompareTo((ulong)y.Value);
    }
}
