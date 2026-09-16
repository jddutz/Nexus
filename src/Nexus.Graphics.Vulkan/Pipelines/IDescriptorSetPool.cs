using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Allocates, writes, and releases Vulkan descriptor sets.
/// </summary>
/// <remarks>
/// Operates entirely on Vulkan resources. It has no knowledge of textures, materials,
/// cameras, or other engine-level resource descriptions.
/// </remarks>
public interface IDescriptorSetPool : IDisposable
{
    /// <summary>
    /// Allocates a descriptor set compatible with the supplied layout.
    /// </summary>
    /// <param name="layout">The descriptor-set layout the allocation must be compatible with.</param>
    /// <returns>The allocated descriptor set.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when allocation fails, including when the pool is exhausted.
    /// </exception>
    DescriptorSet Allocate(DescriptorSetLayout layout);

    /// <summary>
    /// Writes a uniform-buffer descriptor into an existing descriptor-set allocation.
    /// </summary>
    /// <param name="descriptorSet">The descriptor set to update.</param>
    /// <param name="binding">The binding index within the descriptor set.</param>
    /// <param name="buffer">The uniform buffer to bind.</param>
    /// <param name="offset">The offset, in bytes, into <paramref name="buffer"/>.</param>
    /// <param name="range">The number of bytes accessible to the shader.</param>
    void WriteUniformBuffer(
        DescriptorSet descriptorSet,
        uint binding,
        VkBuffer buffer,
        ulong offset,
        ulong range
    );

    /// <summary>
    /// Writes a combined-image-sampler descriptor into an existing descriptor-set allocation.
    /// </summary>
    /// <param name="descriptorSet">The descriptor set to update.</param>
    /// <param name="binding">The binding index within the descriptor set.</param>
    /// <param name="imageView">The image view to bind.</param>
    /// <param name="sampler">The sampler to bind.</param>
    void WriteCombinedImageSampler(
        DescriptorSet descriptorSet,
        uint binding,
        ImageView imageView,
        Sampler sampler
    );

    /// <summary>
    /// Releases a descriptor set back to the pool it was allocated from.
    /// </summary>
    /// <param name="descriptorSet">The descriptor set to release.</param>
    void Release(DescriptorSet descriptorSet);
}
