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
    /// Releases a reference to the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource whose buffer reference is released.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    IEnumerable<IVulkanCommand> Release(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Releases every managed geometry buffer.
    /// </summary>
    void Reset();
}
