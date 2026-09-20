namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Batch strategy that performs depth sorting for correct transparency rendering.
/// Sorts by render priority first, then by depth (back-to-front), then by state changes for batching.
/// Use this for render passes that contain transparent objects requiring correct alpha blending.
/// </summary>
/// <remarks>
/// <para><strong>Sorting Order:</strong></para>
/// <list type="number">
/// <item><b>RenderPriority</b> - Ensures intentional layering (particles before glass before UI)</item>
/// <item><b>DepthSortKey</b> - Back-to-front ordering for correct transparency (higher depth = farther = renders first)</item>
/// <item><b>Pipeline/DescriptorSet/Buffers</b> - Batching optimization within same priority/depth</item>
/// </list>
///
/// <para><strong>Component Usage:</strong></para>
/// Components should calculate DepthSortKey in GetDrawCommands(RenderContext) using the camera position:
/// <code>
/// public override IEnumerable&lt;DrawCommand&gt; GetDrawCommands(RenderContext context)
/// {
///     float depthSortKey = 0f;
///     if (context.Camera != null)
///     {
///         // Calculate distance squared from camera for depth sorting
///         depthSortKey = Vector3D.DistanceSquared(myPosition, context.Camera.Position);
///     }
///
///     yield return new DrawCommand
///     {
///         DepthSortKey = depthSortKey,
///         // ... other properties
///     };
/// }
/// </code>
///
/// <para><strong>Performance Note:</strong></para>
/// Depth sorting prevents efficient batching since objects are sorted by distance rather than state.
/// Only use this strategy for render passes that require transparency.
/// For opaque geometry, use <see cref="DefaultBatchStrategy"/> instead.
/// </remarks>
public class DepthSortBatchStrategy : IBatchStrategy
{
    /// <summary>
    /// Compares two draw commands with depth sorting for transparency.
    /// Prioritizes correctness (priority, depth) over performance (batching).
    /// </summary>
    /// <param name="x">First draw command to compare</param>
    /// <param name="y">Second draw command to compare</param>
    /// <returns>-1 if x should render before y, 1 if y should render before x, 0 if equal priority</returns>
    public int Compare(IRenderItem? x, IRenderItem? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x is null)
            return -1;

        if (y is null)
            return 1;

        // Sort by render priority first (lower priority renders first)
        // This ensures correct layering (e.g., particles → glass → UI)
        var priorityCompare = x.RenderPriority.CompareTo(y.RenderPriority);
        if (priorityCompare != 0)
            return priorityCompare;

        // Within same priority, sort by depth (back-to-front for transparency)
        // IMPORTANT: Higher DepthSortKey = farther from camera = renders FIRST
        // This ensures far objects are behind near objects when alpha blending
        var depthCompare = y.DepthSortKey.CompareTo(x.DepthSortKey); // Note: y compared to x (reversed)
        if (depthCompare != 0)
            return depthCompare;

        // Within same priority and depth, optimize for batching to minimize state changes

        // Sort by pipeline (most expensive to change)
        var pipelineCompare = CompareHandles(x.Pipelines, y.Pipelines, value => value.Handle);
        if (pipelineCompare != 0)
            return pipelineCompare;

        // Then by descriptor set (textures/uniforms)
        var descriptorCompare = CompareDescriptorSets(x.DescriptorSets, y.DescriptorSets);
        if (descriptorCompare != 0)
            return descriptorCompare;

        // Then by vertex buffer
        var vertexCompare = CompareHandles(x.VertexBuffers, y.VertexBuffers, value => value.Handle);
        if (vertexCompare != 0)
            return vertexCompare;

        return 0;
    }

    /// <summary>
    /// Gets a stable hash code for the draw command to enable efficient batch grouping.
    /// Hash includes render priority but NOT depth (depth changes per-frame with camera movement).
    /// </summary>
    /// <param name="state">Draw command to hash</param>
    /// <returns>Hash code representing the batchable aspects of the draw command</returns>
    public int GetHashCode(IRenderItem state)
    {
        var hash = new HashCode();

        // Add RenderPriority (stable across frames)
        hash.Add(state.RenderPriority);

        // Add state change costs
        AddHandles(hash, state.Pipelines, value => value.Handle);
        AddDescriptorSetHandles(hash, state.DescriptorSets);
        AddHandles(hash, state.VertexBuffers, value => value.Handle);

        // NOTE: DepthSortKey deliberately excluded - it's camera-relative and changes every frame
        // Including it would prevent any hash-based caching or grouping optimizations

        return hash.ToHashCode();
    }

    private static int CompareHandles<T>(T[] left, T[] right, Func<T, ulong> getHandle)
    {
        var count = Math.Min(left.Length, right.Length);
        for (var index = 0; index < count; index++)
        {
            var comparison = getHandle(left[index]).CompareTo(getHandle(right[index]));
            if (comparison != 0)
                return comparison;
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int CompareDescriptorSets(DescriptorSet[][] left, DescriptorSet[][] right)
    {
        var count = Math.Min(left.Length, right.Length);
        for (var index = 0; index < count; index++)
        {
            var comparison = CompareHandles(left[index], right[index], value => value.Handle);
            if (comparison != 0)
                return comparison;
        }

        return left.Length.CompareTo(right.Length);
    }

    private static void AddDescriptorSetHandles(HashCode hash, DescriptorSet[][] values)
    {
        foreach (var setArray in values)
        foreach (var descriptorSet in setArray)
            hash.Add(descriptorSet.Handle);
    }

    private static void AddHandles<T>(HashCode hash, T[] values, Func<T, ulong> getHandle)
    {
        foreach (var value in values)
            hash.Add(getHandle(value));
    }
}
