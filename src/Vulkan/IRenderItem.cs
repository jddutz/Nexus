using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Defines the immutable render state used to order and batch render items.
/// </summary>
public interface IRenderItem
{
    /// <summary>
    /// Gets the graphics pipeline used to draw the item.
    /// </summary>
    Pipeline Pipeline { get; }

    /// <summary>
    /// Gets the layout associated with <see cref="Pipeline"/>.
    /// </summary>
    PipelineLayout Layout { get; }

    /// <summary>
    /// Gets the vertex buffer containing the item's geometry.
    /// </summary>
    VkBuffer VertexBuffer { get; }

    /// <summary>
    /// Gets the native descriptor set handle bound while drawing.
    /// </summary>
    ulong DescriptorSetId { get; }

    /// <summary>
    /// Gets the native index buffer handle, or zero when no index buffer is used.
    /// </summary>
    ulong IndexBufferId { get; }

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
