namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Default batching strategy that optimizes Vulkan state changes by grouping render states
/// based on expensive state transitions (framebuffer, shader program, textures, VAO).
/// Prioritizes batches to minimize the most expensive state changes first.
///
/// <para>Optimization Strategy:</para>
/// <list type="bullet">
/// <item><b>Framebuffer binding</b> - Most expensive; off-screen targets rendered before main framebuffer</item>
/// <item><b>Render priority</b> - Secondary sort for layering (background → 3D → UI)</item>
/// <item><b>Shader program</b> - Third priority; groups objects using same shaders</item>
/// <item><b>Textures and VAO</b> - Included in hash for fine-grained batching</item>
/// </list>
///
/// <para>Hash-based change detection allows the renderer to detect when Vulkan state updates are needed between consecutive render states.</para>
/// </summary>
public class DefaultBatchStrategy : IBatchStrategy
{
    /// <summary>
    /// Analyzes a sorted list of draw commands and returns batching statistics.
    /// Call this after sorting to get diagnostic information about batching effectiveness.
    /// </summary>
    public BatchingStatistics AnalyzeBatching(IEnumerable<IRenderItem> sortedCommands)
    {
        var stats = new BatchingStatistics();
        IRenderItem? previous = null;

        foreach (var cmd in sortedCommands)
        {
            stats.TotalDrawCommands++;

            if (previous == null)
            {
                // First command - count all as changes
                stats.PipelineChanges++;
                stats.DescriptorSetChanges++;
                stats.VertexBufferChanges++;
            }
            else
            {
                // Compare with previous to detect state changes
                if (CompareHandles(cmd.Pipelines, previous.Pipelines, value => value.Handle) != 0)
                {
                    stats.PipelineChanges++;
                }

                if (CompareDescriptorSets(cmd.DescriptorSets, previous.DescriptorSets) != 0)
                {
                    stats.DescriptorSetChanges++;
                }

                if (
                    CompareHandles(cmd.VertexBuffers, previous.VertexBuffers, value => value.Handle)
                    != 0
                )
                {
                    stats.VertexBufferChanges++;
                }
            }

            previous = cmd;
        }

        return stats;
    }

    /// <summary>
    /// Statistics about batching effectiveness.
    /// </summary>
    public struct BatchingStatistics
    {
        /// <summary>Gets or sets the number of draw commands analyzed.</summary>
        public int TotalDrawCommands { get; set; }

        /// <summary>Gets or sets the number of pipeline changes between consecutive commands.</summary>
        public int PipelineChanges { get; set; }

        /// <summary>Gets or sets the number of descriptor-set changes between consecutive commands.</summary>
        public int DescriptorSetChanges { get; set; }

        /// <summary>Gets or sets the number of vertex-buffer changes between consecutive commands.</summary>
        public int VertexBufferChanges { get; set; }

        /// <summary>
        /// Gets the batching efficiency (lower is better).
        /// Perfect batching = 1 (all commands use same state).
        /// </summary>
        public float GetBatchingRatio() =>
            TotalDrawCommands > 0 ? (float)PipelineChanges / TotalDrawCommands : 0f;

        /// <summary>Returns a readable summary of the batching statistics.</summary>
        /// <returns>A formatted summary of the analyzed commands and state changes.</returns>
        public override string ToString()
        {
            if (TotalDrawCommands == 0)
                return "No draw commands";

            var ratio = GetBatchingRatio();
            var efficiency = (1f - ratio) * 100f;

            return $"Draw Commands: {TotalDrawCommands}, "
                + $"Pipeline Changes: {PipelineChanges}, "
                + $"Descriptor Changes: {DescriptorSetChanges}, "
                + $"Vertex Buffer Changes: {VertexBufferChanges}, "
                + $"Batching Efficiency: {efficiency:F1}%";
        }
    }

    /// <summary>
    /// Compares two draw commands for batch priority ordering.
    /// Sorts by render priority first (correctness), then by state changes (performance).
    /// This ensures correct layering while minimizing Vulkan state changes.
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
        // This ensures correct layering (e.g., background → scene → UI)
        var priorityCompare = x.RenderPriority.CompareTo(y.RenderPriority);
        if (priorityCompare != 0)
            return priorityCompare;

        // Within the same priority, optimize for batching to minimize state changes
        // Order by cost: pipeline (most expensive) → descriptor set → vertex buffer → push constants (least expensive)

        // 1. Sort by pipeline (most expensive to change)
        var pipelineCompare = CompareHandles(x.Pipelines, y.Pipelines, value => value.Handle);
        if (pipelineCompare != 0)
            return pipelineCompare;

        // 2. Then by descriptor set (textures/uniforms)
        var descriptorCompare = CompareDescriptorSets(x.DescriptorSets, y.DescriptorSets);
        if (descriptorCompare != 0)
            return descriptorCompare;

        // 3. Then by vertex buffer
        var vertexCompare = CompareHandles(x.VertexBuffers, y.VertexBuffers, value => value.Handle);
        if (vertexCompare != 0)
            return vertexCompare;

        // 4. Finally by push constants (least expensive, already part of draw call)
        // This prevents SortedSet from treating commands with different push constants as duplicates
        // Using GetHashCode() works for both value types and reference types
        if (x.PushConstants != null && y.PushConstants != null)
        {
            return x.PushConstants.GetHashCode().CompareTo(y.PushConstants.GetHashCode());
        }
        else if (x.PushConstants != null)
        {
            return 1; // x has push constants, y doesn't -> x comes after
        }
        else if (y.PushConstants != null)
        {
            return -1; // y has push constants, x doesn't -> y comes after
        }

        return 0; // Truly identical commands
    }

    /// <summary>
    /// Gets a stable hash code for the draw command to enable efficient batch grouping.
    /// Hash encodes render priority and state change costs.
    /// Commands with similar hashes will batch together.
    /// Note: DepthSortKey is NOT included as it changes per-frame based on camera position.
    /// </summary>
    /// <param name="state">Draw command to hash</param>
    /// <returns>Hash code representing the batchable aspects of the draw command</returns>
    public int GetHashCode(IRenderItem state)
    {
        var hash = new HashCode();

        // Add RenderPriority first (correctness requirement)
        hash.Add(state.RenderPriority);

        // Then add in order of state change cost (most expensive first)
        AddHandles(hash, state.Pipelines, value => value.Handle);
        AddDescriptorSetHandles(hash, state.DescriptorSets);
        AddHandles(hash, state.VertexBuffers, value => value.Handle);

        // NOTE: DepthSortKey deliberately excluded - it's camera-relative and changes every frame

        return hash.ToHashCode();
    }

    /// <summary>Compares two handle arrays lexicographically.</summary>
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

    /// <summary>Adds the handles in an array to a hash accumulator.</summary>
    private static void AddHandles<T>(HashCode hash, T[] values, Func<T, ulong> getHandle)
    {
        foreach (var value in values)
            hash.Add(getHandle(value));
    }

    /// <summary>Compares descriptor-set arrays lexicographically by Vulkan handle.</summary>
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

    /// <summary>Adds descriptor-set handles to a hash accumulator.</summary>
    private static void AddDescriptorSetHandles(HashCode hash, DescriptorSet[][] values)
    {
        foreach (var setArray in values)
            AddHandles(hash, setArray, value => value.Handle);
    }

    /// <summary>
    /// Computes a consistent hash code for the given Vulkan state parameters.
    /// This method ensures that equivalent render states produce identical hash codes.
    /// produce identical hash codes when the states are equivalent.
    /// </summary>
    /// <param name="framebuffer">Framebuffer identifier or null for the default framebuffer.</param>
    /// <param name="shaderProgram">Shader program identifier or null when no program is bound.</param>
    /// <param name="vertexArray">Vertex-array identifier or null when no vertex array is bound.</param>
    /// <param name="boundTextures">Bound texture identifiers for each texture unit.</param>
    /// <returns>Consistent hash code for the given state parameters</returns>
    private static int ComputeStateHash(
        uint? framebuffer,
        uint? shaderProgram,
        uint? vertexArray,
        uint?[] boundTextures
    )
    {
        var hash = new HashCode();

        // Include framebuffer (most important for batching)
        hash.Add(framebuffer);

        // Include shader program (second most important)
        hash.Add(shaderProgram);

        // Include VAO
        hash.Add(vertexArray);

        // Include all bound textures in order
        foreach (var texture in boundTextures)
        {
            hash.Add(texture);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Compares framebuffer identifiers with special handling for the default framebuffer.
    /// Off-screen framebuffers (non-null) should render before the default framebuffer (null).
    /// </summary>
    private static int CompareFramebuffers(uint? framebuffer1, uint? framebuffer2)
    {
        // Both are default framebuffer
        if (framebuffer1 == null && framebuffer2 == null)
            return 0;

        // framebuffer1 is default, framebuffer2 is off-screen -> framebuffer2 renders first
        if (framebuffer1 == null)
            return 1;

        // framebuffer2 is default, framebuffer1 is off-screen -> framebuffer1 renders first
        if (framebuffer2 == null)
            return -1;

        // Both are off-screen framebuffers, sort by ID for consistency
        return framebuffer1.Value.CompareTo(framebuffer2.Value);
    }

    /// <summary>
    /// Compares two nullable unsigned integers, treating null as less than any actual value.
    /// </summary>
    private static int CompareNullableUInt(uint? value1, uint? value2)
    {
        if (value1 == null && value2 == null)
            return 0;
        if (value1 == null)
            return -1;
        if (value2 == null)
            return 1;
        return value1.Value.CompareTo(value2.Value);
    }
}
