using Nexus.Core.Events;

namespace Nexus.Graphics.Events;

/// <summary>
/// Indicates that an application window's logical size has changed.
/// </summary>
/// <param name="windowId">The identifier of the resized window.</param>
/// <param name="size">The new logical window size in pixels.</param>
public sealed class WindowResizedEvent(WindowId windowId, Vector2D<int> size) : IEvent
{
    /// <summary>
    /// Gets the identifier of the resized window.
    /// </summary>
    public WindowId WindowId { get; } = windowId;

    /// <summary>
    /// Gets the new logical window size in pixels.
    /// </summary>
    public Vector2D<int> Size { get; } = size;
}
