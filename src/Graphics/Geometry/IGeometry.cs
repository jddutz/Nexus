namespace Nexus.Graphics.Geometry;

/// <summary>
/// Defines the data and serialization contract for a geometry resource.
/// </summary>
public interface IGeometry
{
    /// <summary>
    /// Gets the unique identifier of the geometry.
    /// </summary>
    MeshId Id { get; }

    /// <summary>
    /// Gets the display name of the geometry.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the primitive topology used by the geometry.
    /// </summary>
    PrimitiveTopologyEnum Topology { get; }

    /// <summary>
    /// Gets the number of vertices in the geometry.
    /// </summary>
    ulong Count { get; }

    /// <summary>
    /// Writes a range of vertices to a byte buffer using the specified format.
    /// </summary>
    /// <param name="start">The zero-based index of the first vertex to write.</param>
    /// <param name="count">The number of vertices to write.</param>
    /// <param name="format">The format used to serialize each vertex.</param>
    /// <param name="target">The destination buffer for the serialized vertices.</param>
    void WriteTo(int start, int count, VertexFormat format, Span<byte> target);
}