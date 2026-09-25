namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands.
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

    /// <summary>
    /// Removes all commands associated with the specified drawable.
    /// </summary>
    /// <param name="drawableId">The drawable identifier.</param>
    void Remove(DrawableId drawableId);

    /// <summary>Removes the command with the specified unique identifier.</summary>
    /// <param name="commandId">The command identifier.</param>
    void RemoveCommand(Guid commandId);

    /// <summary>
    /// Removes all transient commands from the batch.
    /// </summary>
    void Clean();
}
