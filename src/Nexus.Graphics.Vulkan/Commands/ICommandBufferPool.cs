using VkCommandPool = Silk.NET.Vulkan.CommandPool;

namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Owns a fixed set of Vulkan command buffers and manages non-blocking fence-based reuse.
/// </summary>
/// <remarks>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item>Owns command buffers allocated during pool creation.</item>
/// <item>Reuses buffers only after their previous submission fence signals.</item>
/// <item>Resets reusable command buffers before returning them.</item>
/// <item>Releases owned Vulkan resources when disposed.</item>
/// </list>
///
/// <para><strong>Thread Safety:</strong></para>
/// Command pools are NOT thread-safe in Vulkan. For multi-threaded rendering,
/// create one ICommandBufferPool instance per thread. This implementation is designed
/// for single-threaded use per instance.
///
/// <para><strong>Lifecycle:</strong></para>
/// <list type="number">
/// <item>Create the pool during renderer initialization.</item>
/// <item>Acquire a buffer with TryGetCommandBuffer before recording a frame.</item>
/// <item>Skip the frame when all buffers are still associated with unsignaled work.</item>
/// <item>Dispose the pool after all device work using its buffers has completed.</item>
/// </list>
///
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// // Create a fixed pool and acquire a buffer when a frame is ready.
/// var pool = CommandPool.ForGraphics(context, 2, allowIndividualReset: true);
/// if (pool.TryGetCommandBuffer(frameFence, out var commandBuffer))
/// {
///     // Record and submit commandBuffer.
/// }
/// </code>
/// </remarks>
public interface ICommandBufferPool : IDisposable
{
    /// <summary>
    /// Attempts to acquire a command buffer whose previous submission has completed.
    /// </summary>
    /// <param name="fence">Fence that governs the buffer's next submission.</param>
    /// <param name="commandBuffer">The acquired command buffer, or the default handle.</param>
    /// <returns>
    /// <see langword="true"/> when a reusable buffer was acquired; otherwise,
    /// <see langword="false"/> when all buffers still have unsignaled work.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Vulkan reports an unexpected fence-status or reset error.
    /// </exception>
    bool TryGetCommandBuffer(Fence fence, out CommandBuffer commandBuffer);

    /// <summary>
    /// Resets all command buffers in this pool to their initial state.
    /// This is an explicit whole-pool operation and is not required for ordinary reuse.
    /// </summary>
    /// <param name="flags">Reset behavior flags</param>
    /// <remarks>
    /// <para><strong>Reset Flags:</strong></para>
    /// <list type="bullet">
    /// <item>None: Default behavior - command buffers can be reused</item>
    /// <item>ReleaseResourcesBit: Releases memory back to system (slower, frees memory)</item>
    /// </list>
    ///
    /// <para>
    /// Call only after all command buffers from this pool have finished executing.
    /// </para>
    ///
    /// <para><strong>Important:</strong></para>
    /// Must wait for all command buffers from this pool to finish executing
    /// before calling Reset(). Use fences or vkDeviceWaitIdle() to ensure safety.
    /// </remarks>
    void Reset(CommandPoolResetFlags flags = CommandPoolResetFlags.None);

    /// <summary>
    /// Trims the native command pool to reduce memory usage.
    /// Releases unused memory allocations back to the device.
    /// </summary>
    /// <remarks>
    /// Optional optimization for long-running applications. Called after resetting
    /// the pool to reclaim memory from temporary allocations. Has a performance cost,
    /// so use sparingly (e.g., after loading screens or level transitions).
    /// </remarks>
    void Trim();
}
