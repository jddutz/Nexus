namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Identifies the swap-chain image acquired for a render frame.
/// </summary>
public readonly record struct RenderFrameResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenderFrameResult"/> struct.
    /// </summary>
    /// <param name="frameIndex">The index of the frame synchronization slot.</param>
    /// <param name="imageIndex">The index of the acquired swap-chain image.</param>
    public RenderFrameResult(int frameIndex, uint imageIndex)
    {
        FrameIndex = frameIndex;
        ImageIndex = imageIndex;
    }

    /// <summary>
    /// Gets the index of the frame synchronization slot used for the render frame.
    /// </summary>
    public int FrameIndex { get; }

    /// <summary>
    /// Gets the index of the acquired swap-chain image.
    /// </summary>
    public uint ImageIndex { get; }
}
