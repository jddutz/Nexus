namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Defines the resolved Vulkan render state used to render an item across its supported passes.
/// </summary>
public interface IRenderItem
{
    /// <summary>
    /// Gets the render passes in which this item participates.
    /// </summary>
    uint RenderPassMask { get; }

    /// <summary>
    /// Gets the graphics pipeline used for each render pass.
    /// </summary>
    Pipeline[] Pipelines { get; }

    /// <summary>
    /// Gets the pipeline layout used for each render pass.
    /// </summary>
    PipelineLayout[] Layouts { get; }

    /// <summary>
    /// Gets the vertex buffer used for each render pass.
    /// </summary>
    VkBuffer[] VertexBuffers { get; }

    /// <summary>
    /// Gets the descriptor sets used for each render pass, ordered by Vulkan set number.
    /// </summary>
    DescriptorSet[][] DescriptorSets { get; }

    /// <summary>
    /// Gets the optional push constant data sent before drawing.
    /// </summary>
    object? PushConstants { get; }

    /// <summary>
    /// Gets the item's render ordering priority.
    /// </summary>
    int RenderPriority { get; }

    /// <summary>
    /// Gets the camera-relative sorting key used by depth-sorted batches.
    /// </summary>
    float DepthSortKey { get; }
}
