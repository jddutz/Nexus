namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Creates and manages Vulkan vertex buffers from packed mesh definitions.
/// </summary>
public interface IMeshFactory
{
    /// <summary>
    /// Creates a Vulkan vertex buffer for the supplied mesh definition.
    /// </summary>
    /// <param name="definition">The packed mesh data to upload.</param>
    /// <returns>The identifier of the created mesh resource.</returns>
    ResourceId Create(MeshDefinition definition);

    /// <summary>
    /// Gets the Vulkan vertex buffer for a mesh resource.
    /// </summary>
    /// <param name="id">The mesh resource identifier.</param>
    /// <returns>The Vulkan vertex buffer.</returns>
    VkBuffer ReadBuffer(ResourceId id);

    /// <summary>
    /// Gets the vertex count for a mesh resource.
    /// </summary>
    /// <param name="id">The mesh resource identifier.</param>
    /// <returns>The vertex count.</returns>
    uint ReadVertexCount(ResourceId id);

    /// <summary>
    /// Deletes a mesh resource when it exists.
    /// </summary>
    /// <param name="id">The mesh resource identifier.</param>
    /// <returns>The deleted mesh resource identifier.</returns>
    ResourceId Delete(ResourceId id);
}
