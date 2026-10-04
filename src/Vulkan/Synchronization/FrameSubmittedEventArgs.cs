namespace Nexus.Graphics.Vulkan.Synchronization;

/// <summary>
/// Provides the index of a frame slot whose commands were submitted to the graphics queue.
/// </summary>
/// <param name="frameIndex">The submitted frame slot index.</param>
public sealed class FrameSubmittedEventArgs(uint frameIndex) : EventArgs
{
    /// <summary>Gets the submitted frame slot index.</summary>
    public uint FrameIndex { get; } = frameIndex;
}
