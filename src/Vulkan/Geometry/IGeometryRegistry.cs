/// <summary>
/// Manages Vulkan vertex buffers for serialized geometry resources.
/// </summary>
public interface IGeometryRegistry : IDisposable
{
    /// <summary>
    /// Creates or references the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource to serialize.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    void Create(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Recreates the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource to serialize.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    void Update(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Releases a reference to the buffer for a geometry resource in the specified vertex format.
    /// </summary>
    /// <param name="geometry">The geometry resource whose buffer reference is released.</param>
    /// <param name="format">The vertex format used to serialize the geometry.</param>
    void Release(IGeometry geometry, VertexFormat format);

    /// <summary>
    /// Releases every managed geometry buffer.
    /// </summary>
    void Reset();
}
