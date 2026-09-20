namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Provides an abstraction for batching strategies in the rendering pipeline.
/// Implementations define how render items are ordered and grouped to minimize Vulkan state changes.
/// </summary>
public interface IBatchStrategy : IComparer<IRenderItem>
{
    /// <summary>
    /// Computes a stable hash code for the specified <see cref="IRenderItem"/> to facilitate efficient batch grouping.
    /// Render states with the same hash code are considered part of the same batch.
    /// </summary>
    /// <param name="state">The <see cref="IRenderItem"/> to compute the hash code for.</param>
    /// <returns>An integer hash code representing the batchable aspects of the render state.</returns>
    int GetHashCode(IRenderItem state);
}
