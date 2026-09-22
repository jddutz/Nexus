namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands for a rendering operation.
/// </summary>
public class RenderBatch(IBatchStrategy batchStrategy) : IRenderBatch
{
    private SortedSet<IVulkanCommand> _commands = new(batchStrategy);

    /// <summary>
    /// Gets or sets the frame synchronization slot for which this batch was prepared.
    /// </summary>
    public int FrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the swap-chain image associated with this batch.
    /// </summary>
    public VkImage Image { get; set; }

    /// <summary>
    /// Gets or sets the image view associated with the swap-chain image.
    /// </summary>
    public VkImageView ImageView { get; set; }

    /// <summary>
    /// Gets the Vulkan commands in execution order.
    /// </summary>
    public IEnumerable<IVulkanCommand> Commands => _commands;

    /// <summary>
    /// Adds a Vulkan command to the batch.
    /// </summary>
    /// <param name="command">The command to add.</param>
    /// <returns>
    /// <see langword="true"/> if the command was added;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool Add(IVulkanCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _commands.Add(command);
    }

    public void Clear()
    {
        _commands.Clear();
    }
}
