namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines the render state and active passes for a rendering operation.
/// </summary>
public interface IRenderBatch
{
    /// <summary
    /// Adds a RenderItem to the current batch.
    /// </summary>
    RenderItemId Add(RenderItem renderItem);

    /// <summary>
    /// Gets or sets the load operation for the render target.
    /// </summary>
    AttachmentLoadOp LoadOp { get; set; }

    /// <summary>
    /// Gets or sets the viewport used for rendering.
    /// </summary>
    VkViewport Viewport { get; set; }

    /// <summary>
    /// Gets or sets the scissor rectangle used for rendering.
    /// </summary>
    Rect2D Scissor { get; set; }

    /// <summary>
    /// Gets or sets the active render-pass definitions in execution order.
    /// </summary>
    RenderPassDefinition[] RenderPasses { get; set; }

    /// <summary>
    /// Gets the draw commands associated with this render definition.
    /// </summary>
    List<RenderItem> Items { get; set; }
}
