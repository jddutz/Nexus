namespace Nexus.Graphics.Vulkan.Geometry;

/// <summary>
/// Manages Vulkan vertex buffers containing drawable instance data.
/// </summary>
public interface IInstanceBufferRegistry : IDisposable
{
    /// <summary>
    /// Creates or replaces the per-instance vertex buffer for a drawable.
    /// </summary>
    /// <param name="drawable">The drawable that owns the instance buffer.</param>
    /// <param name="layout">The shader inputs that define the instance-buffer layout.</param>
    IEnumerable<IVulkanCommand> Create(IDrawable drawable, ShaderInput[] layout);

    /// <summary>
    /// Gets the registered per-instance vertex buffer for a drawable.
    /// </summary>
    /// <param name="drawableId">The drawable identifier.</param>
    /// <returns>The registered Vulkan instance buffer.</returns>
    /// <exception cref="KeyNotFoundException">
    /// The drawable does not identify a registered instance buffer.
    /// </exception>
    VkBuffer Get(DrawableId drawableId);

    /// <summary>
    /// Releases the per-instance vertex buffer for a drawable.
    /// </summary>
    /// <param name="drawableId">The drawable identifier.</param>
    IEnumerable<IVulkanCommand> Release(DrawableId drawableId);

    /// <summary>
    /// Releases every managed instance buffer.
    /// </summary>
    void Reset();
}
