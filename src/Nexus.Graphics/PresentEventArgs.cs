namespace Nexus.Graphics;

/// <summary>
/// Event arguments for swapchain presentation events.
/// Provides the swapchain image that is about to be presented.
/// </summary>
public class PresentEventArgs : EventArgs
{
    /// <summary>
    /// Gets the index of the swapchain image being presented.
    /// </summary>
    public required uint ImageIndex { get; init; }

    /// <summary>
    /// Gets the backend image handle that is about to be presented.
    /// This image is in PresentSrcKhr layout and ready for readback operations.
    /// </summary>
    public required GpuHandle Image { get; init; }
}
