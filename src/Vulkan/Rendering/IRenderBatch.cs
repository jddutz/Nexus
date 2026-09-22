namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands for a rendering operation.
/// </summary>
public interface IRenderBatch
{
    /// <summary>
    /// Gets or sets the frame synchronization slot for which this batch was prepared.
    /// </summary>
    int FrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the swap-chain image associated with this batch.
    /// </summary>
    VkImage Image { get; set; }

    /// <summary>
    /// Gets or sets the image view associated with the swap-chain image.
    /// </summary>
    VkImageView ImageView { get; set; }

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
    /// Removes all commands associated with a given Drawable.
    /// </summary>
    void Remove(DrawableId drawableId);

    /// <summary
    /// Removes all transient (IsSticky == false) commands from the batch.
    /// </summary>
    void Clean();
}
