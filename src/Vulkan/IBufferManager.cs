namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Creates, updates, and destroys Vulkan buffers and their associated device memory.
/// </summary>
public interface IBufferManager
{
    /// <summary>
    /// Creates a vertex buffer and uploads the supplied vertex data to it.
    /// </summary>
    /// <param name="data">The vertex data to upload.</param>
    /// <returns>The handle of the created vertex buffer.</returns>
    VkBuffer CreateVertexBuffer(ReadOnlySpan<byte> data);

    /// <summary>
    /// Creates an index buffer and uploads the supplied index data to it.
    /// </summary>
    /// <param name="data">The index data to upload.</param>
    /// <returns>The handle of the created index buffer.</returns>
    VkBuffer CreateIndexBuffer(ReadOnlySpan<byte> data);

    /// <summary>
    /// Creates a uniform buffer with the requested capacity.
    /// </summary>
    /// <param name="size">The capacity of the buffer in bytes.</param>
    /// <returns>The handle of the created uniform buffer.</returns>
    VkBuffer CreateUniformBuffer(ulong size);

    /// <summary>
    /// Creates a storage buffer with the requested capacity.
    /// </summary>
    /// <param name="size">The capacity of the buffer in bytes.</param>
    /// <returns>The handle of the created storage buffer.</returns>
    VkBuffer CreateStorageBuffer(ulong size);

    /// <summary>
    /// Replaces the contents of an existing host-visible buffer.
    /// </summary>
    /// <param name="buffer">The handle of the buffer to update.</param>
    /// <param name="data">The data to copy into the buffer.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="buffer"/> is not managed by this instance.</exception>
    void UpdateBuffer(VkBuffer buffer, ReadOnlySpan<byte> data);

    /// <summary>
    /// Releases a buffer and the device memory backing it.
    /// </summary>
    /// <param name="buffer">The handle of the buffer to destroy.</param>
    void DestroyBuffer(VkBuffer buffer);
}
