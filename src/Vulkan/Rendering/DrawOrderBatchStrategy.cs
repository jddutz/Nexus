namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Orders commands by render-pass membership, drawable order, and command state.
/// </summary>
/// <remarks>
/// Render-pass membership is not a depth key. Within the same render pass, draw order
/// is evaluated before pipeline state so different drawable pipelines can interleave.
/// A drawable depth key is not yet available, so actual back-to-front depth sorting
/// remains unimplemented.
/// </remarks>
public sealed class DrawOrderBatchStrategy : IBatchStrategy
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

        if (x.Drawable is null && y.Drawable is not null)
            return -1;

        if (x.Drawable is not null && y.Drawable is null)
            return 1;

        if (x.Drawable is not null && y.Drawable is not null)
        {
            result = x.Drawable.DrawOrder.CompareTo(y.Drawable.DrawOrder);
            if (result != 0)
                return result;
        }

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
