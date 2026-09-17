namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Defines packed mesh data ready for upload to a Vulkan vertex buffer.
/// </summary>
public sealed class MeshDefinition : IResourceDefinition
{
    /// <summary>
    /// Gets the identity of the source mesh data and vertex format.
    /// </summary>
    public ResourceId Id { get; }

    /// <summary>
    /// Gets the packed vertex bytes.
    /// </summary>
    public ReadOnlyMemory<byte> VertexData { get; }

    /// <summary>
    /// Gets the number of vertices in <see cref="VertexData"/>.
    /// </summary>
    public ulong VertexCount { get; }

    /// <summary>
    /// Gets the size in bytes of one packed vertex.
    /// </summary>
    public uint VertexStride { get; }

    /// <summary>
    /// Initializes a packed mesh definition.
    /// </summary>
    /// <param name="sourceId">The identity of the source mesh data.</param>
    /// <param name="format">The format used to pack the vertex data.</param>
    /// <param name="vertexData">The packed vertex bytes.</param>
    /// <param name="vertexCount">The number of packed vertices.</param>
    public MeshDefinition(
        ResourceId sourceId,
        VertexFormat format,
        ReadOnlyMemory<byte> vertexData,
        ulong vertexCount
    )
    {
        ArgumentNullException.ThrowIfNull(format);

        var expectedLength = checked(vertexCount * format.Stride);
        if ((ulong)vertexData.Length != expectedLength)
        {
            throw new ArgumentException(
                "Packed vertex data length must equal the vertex count multiplied by the vertex stride.",
                nameof(vertexData)
            );
        }

        Id = new IdentityHashBuilder(nameof(MeshDefinition))
            .Add(sourceId)
            .Add(format.Id)
            .Compute();
        VertexData = vertexData;
        VertexCount = vertexCount;
        VertexStride = format.Stride;
    }
}