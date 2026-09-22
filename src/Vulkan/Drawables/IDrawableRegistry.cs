namespace Nexus.Graphics.Vulkan.Drawable;

/// <summary>
/// Creates Vulkan commands for drawable resources and manages their lifetime.
/// </summary>
public interface IDrawableRegistry : IDisposable
{
    /// <summary>
    /// Creates commands that add a drawable resource to the rendering system.
    /// </summary>
    /// <param name="drawable">The drawable resource to add.</param>
    /// <returns>The Vulkan commands required to add the drawable resource.</returns>
    IEnumerable<IVulkanCommand> Create(IDrawable drawable);

    /// <summary>
    /// Creates commands that update a drawable resource in the rendering system.
    /// </summary>
    /// <param name="drawable">The drawable resource to update.</param>
    /// <returns>The Vulkan commands required to update the drawable resource.</returns>
    IEnumerable<IVulkanCommand> Update(IDrawable drawable);

    /// <summary>
    /// Creates commands that remove a drawable resource from the rendering system.
    /// </summary>
    /// <param name="drawable">The drawable resource to remove.</param>
    /// <returns>The Vulkan commands required to remove the drawable resource.</returns>
    IEnumerable<IVulkanCommand> Release(IDrawable drawable);

    /// <summary>
    /// Releases all resources managed by the registry.
    /// </summary>
    void Reset();
}
