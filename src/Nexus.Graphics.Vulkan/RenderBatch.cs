using VkViewport = Silk.NET.Vulkan.Viewport;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Defines the render state and active passes for a rendering operation.
/// </summary>
public class RenderBatch
{
    /// <summary>
    /// Gets or sets the load operation for the render target.
    /// </summary>
    public AttachmentLoadOp LoadOp { get; set; }

    /// <summary>
    /// Gets or sets the viewport used for rendering.
    /// </summary>
    public VkViewport Viewport { get; set; }

    /// <summary>
    /// Gets or sets the scissor rectangle used for rendering.
    /// </summary>
    public Rect2D Scissor { get; set; }

    /// <summary>
    /// Gets or sets the active render-pass definitions in execution order.
    /// </summary>
    public RenderPassDefinition[] RenderPasses { get; set; } = [new()];

    /// <summary>
    /// Gets or sets the clear values used by the render passes.
    /// </summary>
    public ClearValue[] ClearValues { get; set; } = [];

    /// <summary>
    /// Gets the draw commands associated with this render definition.
    /// </summary>
    public List<RenderItem> Items { get; set; } = [];
}
