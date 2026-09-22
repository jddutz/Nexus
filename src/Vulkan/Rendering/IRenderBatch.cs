namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands for a rendering operation.
/// </summary>
public interface IRenderBatch
{
    /// <summary>
    /// Adds a Vulkan command to the batch.
    /// </summary>
    /// <param name="command">The command to add.</param>
    /// <returns>
    /// <see langword="true"/> if the command was added;
    /// otherwise, <see langword="false"/> if an equivalent command already exists.
    /// </returns>
    bool Add(IVulkanCommand command);

    /// <summary>
    /// Gets the Vulkan commands in execution order.
    /// </summary>
    IEnumerable<IVulkanCommand> Commands { get; }

    /// <summary
    /// Removes commands that are no longer needed from the batch.
    /// </summary>
    void Clean();
}
