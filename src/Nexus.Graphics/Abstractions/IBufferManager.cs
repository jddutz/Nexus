namespace Nexus.Graphics.Abstractions;

/// <summary>
/// Manages GPU buffer creation and destruction.
/// Encapsulates low-level backend buffer operations for reuse across resource managers.
/// </summary>
public interface IBufferManager
{
    /// <summary>
    /// Creates a vertex buffer and uploads the given data to it.
    /// </summary>
    /// <param name="data">Vertex data to upload to the buffer</param>
    /// <returns>Handle to the created buffer</returns>
    GpuHandle CreateVertexBuffer(ReadOnlySpan<byte> data);

    /// <summary>
    /// Creates a uniform buffer sized for CPU-visible updates.
    /// Uniform buffers are used for passing data to shaders via descriptor sets.
    /// </summary>
    /// <param name="size">Size of the uniform buffer in bytes</param>
    /// <returns>Handle to the created buffer</returns>
    /// <remarks>
    /// Uniform buffers are created with host-visible, host-coherent memory,
    /// allowing them to be mapped and updated from CPU without explicit synchronization.
    /// Use UpdateUniformBuffer to update the buffer contents.
    /// </remarks>
    GpuHandle CreateUniformBuffer(ulong size);

    /// <summary>
    /// Updates the contents of a uniform buffer.
    /// </summary>
    /// <param name="buffer">Handle of the uniform buffer to update</param>
    /// <param name="data">Data to write to the buffer</param>
    /// <remarks>
    /// The buffer must have been created with host-visible memory.
    /// This method maps the memory, copies the data, and unmaps.
    /// </remarks>
    void UpdateUniformBuffer(GpuHandle buffer, ReadOnlySpan<byte> data);

    /// <summary>
    /// Destroys a buffer and frees its associated device memory.
    /// </summary>
    /// <param name="buffer">Buffer to destroy</param>
    void DestroyBuffer(GpuHandle buffer);
}
