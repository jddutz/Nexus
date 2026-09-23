namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines the contract for a renderer that records and submits Vulkan workloads.
/// </summary>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Prepares the renderer to record a new frame.
    /// </summary>
    /// <param name="batch">Commands to record before processing render layers.</param>
    /// <returns>
    /// The acquired frame and its swap-chain image index, or <see langword="null"/> if
    /// rendering cannot begin.
    /// </returns>
    RenderFrameResult? PrepareFrame(IRenderBatch batch);

    /// <summary>
    /// Begins processing a render layer.
    /// </summary>
    /// <param name="batch">Commands to record before processing the layer's workloads.</param>
    void Begin(IRenderBatch batch);

    /// <summary>
    /// Records a compute workload.
    /// </summary>
    /// <param name="batch">The compute commands to record.</param>
    void Compute(IRenderBatch batch);

    /// <summary>
    /// Records a render pass.
    /// </summary>
    /// <param name="batch">The rendering commands to record within the render pass.</param>
    void Record(int renderPassIndex, IRenderBatch batch);

    /// <summary>
    /// Finalizes processing of a render layer.
    /// </summary>
    /// <param name="batch">Commands to record after processing the layer's workloads.</param>
    void Finalize(IRenderBatch batch);

    /// <summary>
    /// Ends command recording, submits the current frame, and presents it.
    /// </summary>
    void Submit();
}
