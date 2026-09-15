namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Defines the lifecycle for creating and removing render-item registrations for graphics components.
/// </summary>
public interface IComponentRegistry
{
    /// <summary>
    /// Determines whether this registry supports the specified graphics component.
    /// </summary>
    /// <param name="component">The graphics component to evaluate.</param>
    /// <returns><see langword="true"/> when the component can be loaded; otherwise, <see langword="false"/>.</returns>
    bool CanLoad(IGraphicsComponent component);

    /// <summary>
    /// Creates or retrieves the render items required by the specified graphics component.
    /// </summary>
    /// <param name="component">The graphics component to load.</param>
    /// <returns>The render items to which the component was registered.</returns>
    RenderItem[] Load(IGraphicsComponent component);

    /// <summary>
    /// Determines whether this registry has a registration for the specified component.
    /// </summary>
    /// <param name="componentId">The identifier of the component to evaluate.</param>
    /// <returns><see langword="true"/> when the component can be unloaded; otherwise, <see langword="false"/>.</returns>
    bool CanUnload(ComponentId componentId);

    /// <summary>
    /// Removes the render-item registrations for the specified component.
    /// </summary>
    /// <param name="componentId">The identifier of the component to unload.</param>
    void Unload(ComponentId componentId);
}
