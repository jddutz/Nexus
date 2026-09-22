namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines the contract for a renderer that executes Vulkan rendering operations.
/// </summary>
public interface IRenderer
{
    /// <summary>
    /// Prepares the renderer to record a new frame.
    /// </summary>
    /// <returns>
    /// The acquired frame and its swap-chain image index, or <see langword="null"/> if
    /// rendering cannot begin.
    /// </returns>
    RenderFrameResult? Begin();

    /// <summary>
    /// Records the commands required to render a batch into the current command buffer.
    /// </summary>
    /// <param name="batch">The batch of render state, passes, and items to record.</param>
    void Record(IRenderBatch batch);

    /// <summary>
    /// Ends command recording and submits the current frame to the graphics queue.
    /// </summary>
    void Submit();
}
