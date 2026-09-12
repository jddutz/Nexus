namespace Nexus.Graphics;

/// <summary>
/// Immutable record representing graphics rendering state for a viewport.
/// Contains only essential viewport state - no lifecycle management or component references.
/// Cameras create viewports, ContentManager tracks cameras, Renderer uses viewports for rendering.
/// </summary>
public record Viewport
{
    /// <summary>
    /// Gets the extent (width and height in pixels) of the viewport.
    /// </summary>
    public required ViewportExtent Extent { get; init; }

    /// <summary>
    /// Gets the clear color for the viewport framebuffer.
    /// </summary>
    public required Vector4D<float> BackgroundColor { get; init; }

    /// <summary>
    /// Gets the render pass mask determining which render passes this viewport participates in.
    /// Defaults to All if not specified.
    /// </summary>
    public uint RenderPassMask { get; init; } = uint.MaxValue;
}
