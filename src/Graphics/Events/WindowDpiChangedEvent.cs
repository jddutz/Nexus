using Nexus.Core.Events;

namespace Nexus.Graphics.Events;

/// <summary>
/// Indicates that a window's framebuffer-to-window DPI scale has changed.
/// </summary>
/// <param name="windowId">The identifier of the affected window.</param>
/// <param name="scale">The new horizontal and vertical DPI scale factors.</param>
public sealed class WindowDpiChangedEvent(WindowId windowId, Vector2D<float> scale) : IEvent
{
    /// <summary>
    /// Gets the identifier of the affected window.
    /// </summary>
    public WindowId WindowId { get; } = windowId;

    /// <summary>
    /// Gets the new horizontal and vertical DPI scale factors.
    /// </summary>
    public Vector2D<float> Scale { get; } = scale;
}