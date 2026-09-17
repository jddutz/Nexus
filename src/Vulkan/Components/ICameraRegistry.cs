namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Owns the realized Vulkan GPU state (descriptor-set layout, view-projection uniform buffer, and
/// set-0 descriptor set) for active cameras. Never calculates camera matrices; it only uploads the
/// values already exposed by <see cref="ICameraComponent.ViewProjectionMatrix"/>.
/// </summary>
public interface ICameraRegistry : IDisposable
{
    /// <summary>
    /// Gets the descriptor set of the currently active camera, or <see langword="null"/> when no
    /// camera is registered.
    /// </summary>
    DescriptorSet? ActiveCameraDescriptorSet { get; }

    /// <summary>
    /// Registers a camera and becomes the active camera, creating its descriptor-set layout (on
    /// first ever registration), uniform buffer, and descriptor set (on first registration of this
    /// camera). Idempotent by <see cref="IComponent.Id"/>.
    /// </summary>
    /// <param name="camera">The camera component to register.</param>
    void Register(ICameraComponent camera);

    /// <summary>
    /// Uploads the camera's current <see cref="ICameraComponent.ViewProjectionMatrix"/> into its
    /// uniform buffer. No-op when the camera is not registered.
    /// </summary>
    /// <param name="camera">The camera whose matrix should be re-uploaded.</param>
    void Update(ICameraComponent camera);

    /// <summary>
    /// Releases the descriptor set and uniform buffer for the specified camera. Safe to call when
    /// the camera is not registered.
    /// </summary>
    /// <param name="componentId">The identifier of the camera to remove.</param>
    void Remove(ComponentId componentId);

    /// <summary>
    /// Releases every registered camera's descriptor set and uniform buffer and clears the
    /// registry. Does not destroy pipeline-owned descriptor layouts or descriptor pools.
    /// </summary>
    void Purge();
}
