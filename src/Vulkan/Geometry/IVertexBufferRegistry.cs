namespace Nexus.Graphics.Vulkan.Geometry;

/// <summary>
/// Manages Vulkan vertex buffers for serialized geometry resources.
/// </summary>
public interface IVertexBufferRegistry : IDisposable
{
    /// <summary>
    /// Creates or references the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource to serialize.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    IEnumerable<IVulkanCommand> Create(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Creates or replaces the per-instance vertex buffer for a drawable.
    /// </summary>
    /// <param name="drawable">The drawable that owns the instance buffer.</param>
    /// <param name="layout">The shader inputs that define the instance-buffer layout.</param>
    IEnumerable<IVulkanCommand> CreateInstanceBuffer(IDrawable drawable, ShaderInput[] layout);

    /// <summary>
    /// Recreates the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource to serialize.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    IEnumerable<IVulkanCommand> Update(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Gets the registered Vulkan vertex buffer for a geometry and vertex format.
    /// </summary>
    /// <param name="meshId">The geometry identifier.</param>
    /// <param name="formatId">The vertex-format identifier.</param>
    /// <returns>The registered Vulkan vertex buffer.</returns>
    /// <exception cref="KeyNotFoundException">
    /// The geometry and vertex format do not identify a registered buffer.
    /// </exception>
    VkBuffer Get(MeshId meshId, VertexFormatId formatId);

    /// <summary>
    /// Gets the registered per-instance vertex buffer for a drawable.
    /// </summary>
    /// <param name="drawableId">The drawable identifier.</param>
    /// <returns>The registered Vulkan instance buffer.</returns>
    /// <exception cref="KeyNotFoundException">
    /// The drawable does not identify a registered instance buffer.
    /// </exception>
    VkBuffer GetInstanceBuffer(DrawableId drawableId);

    /// <summary>
    /// Releases a reference to the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource whose buffer reference is released.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    IEnumerable<IVulkanCommand> Release(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Releases the per-instance vertex buffer for a drawable.
    /// </summary>
    /// <param name="drawableId">The drawable identifier.</param>
    IEnumerable<IVulkanCommand> ReleaseInstanceBuffer(DrawableId drawableId);

    /// <summary>
    /// Releases every managed geometry buffer.
    /// </summary>
    void Reset();
}
