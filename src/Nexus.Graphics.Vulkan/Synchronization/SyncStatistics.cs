namespace Nexus.Graphics.Vulkan.Synchronization;

/// <summary>
/// Statistics about synchronization primitive usage and performance.
/// Useful for debugging frame pacing, detecting stalls, and optimizing GPU utilization.
/// </summary>
public record SyncStatistics
{
    /// <summary>
    /// Maximum number of frames that can be in flight simultaneously.
    /// </summary>
    public uint MaxFramesInFlight { get; init; }

    /// <summary>
    /// Total number of fence wait operations.
    /// </summary>
    public ulong TotalFenceWaits { get; init; }

    /// <summary>
    /// Number of times fence wait timed out.
    /// Non-zero indicates GPU stalls or hangs.
    /// </summary>
    public ulong FenceWaitTimeouts { get; init; }

    /// <summary>
    /// Total number of fence reset operations.
    /// Should roughly match TotalFenceWaits.
    /// </summary>
    public ulong TotalFenceResets { get; init; }

    /// <summary>
    /// Total time spent waiting for fences (milliseconds).
    /// High values indicate CPU waiting for GPU (GPU bottleneck).
    /// </summary>
    public double TotalFenceWaitTimeMs { get; init; }

    /// <summary>
    /// Average time spent waiting for fences (milliseconds).
    /// </summary>
    public double AverageFenceWaitTimeMs =>
        TotalFenceWaits > 0 ? TotalFenceWaitTimeMs / TotalFenceWaits : 0;

    /// <summary>
    /// Number of times DeviceWaitIdle was called.
    /// Should be rare - frequent calls indicate architectural problems.
    /// </summary>
    public ulong DeviceWaitIdleCalls { get; init; }

    /// <summary>
    /// Number of times QueueWaitIdle was called.
    /// Should also be rare.
    /// </summary>
    public ulong QueueWaitIdleCalls { get; init; }

    /// <summary>
    /// Current frame index being rendered.
    /// Cycles through [0, MaxFramesInFlight).
    /// </summary>
    public uint CurrentFrameIndex { get; init; }

    /// <summary>
    /// Total number of frames rendered since application start.
    /// </summary>
    public ulong TotalFramesRendered { get; init; }

    /// <summary>
    /// Number of active semaphores (ImageAvailable + RenderFinished per frame).
    /// Should be MaxFramesInFlight * 2.
    /// </summary>
    public uint ActiveSemaphoreCount { get; init; }

    /// <summary>
    /// Number of active fences (one per frame).
    /// Should equal MaxFramesInFlight.
    /// </summary>
    public uint ActiveFenceCount { get; init; }
}
