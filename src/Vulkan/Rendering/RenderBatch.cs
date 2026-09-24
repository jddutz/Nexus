namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands.
/// </summary>
public class RenderBatch(IBatchStrategy batchStrategy) : IRenderBatch
{
    private readonly SortedSet<IVulkanCommand> _commands = new(batchStrategy);

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

        var added = _commands.Add(command);
        if (!added)
        {
            Debug.WriteLine(
                $"[WARN] Command collapsed by batch comparer. CommandType={command.GetType().Name}, PipelineId={command.PipelineId}, "
                    + $"DrawableId={command.Drawable?.Id}, RenderPriority={command.RenderPriority}, CommandId={command.Id}"
            );
        }

        return added;
    }

    /// <inheritdoc />
    public void Remove(DrawableId drawableId)
    {
        _commands.RemoveWhere(command => command.Drawable?.Id == drawableId);
    }

    /// <inheritdoc />
    public void Clean()
    {
        _commands.RemoveWhere(command => !command.IsSticky);
    }
}
