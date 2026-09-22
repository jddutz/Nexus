namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands for a rendering operation.
/// </summary>
public class RenderBatch(IBatchStrategy batchStrategy) : IRenderBatch
{
    private SortedSet<IVulkanCommand> _commands = new(batchStrategy);

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

    public void Clean()
    {
        _commands.RemoveWhere(cmd => cmd.RefCount <= 0 || (!cmd.IsSticky && cmd.IsRecorded));
    }
}
