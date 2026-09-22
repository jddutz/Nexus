namespace Nexus.Graphics.Vulkan.Synchronization;

/// <summary>
/// Provides the index of a frame slot whose GPU work has completed.
/// </summary>
/// <param name="frameIndex">The completed frame slot index.</param>
public sealed class FrameCompletedEventArgs(uint frameIndex) : EventArgs
{
    /// <summary>
    /// Gets the completed frame slot index.
    /// </summary>
    public uint FrameIndex { get; } = frameIndex;
}
