namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Defines an ordered collection of Vulkan commands.
/// </summary>
public class RenderBatch(IBatchStrategy batchStrategy) : IRenderBatch
{
    // DrawOrder and other comparer inputs can change while commands are retained.
    // Stable identity must control membership; sort only when reading the batch.
    private readonly Dictionary<Guid, IVulkanCommand> _commands = [];

    /// <summary>
    /// Gets the Vulkan commands in execution order.
    /// </summary>
    public IEnumerable<IVulkanCommand> Commands =>
        _commands.Values.OrderBy(command => command, batchStrategy);

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

        // A replacement instance buffer must supersede the old binding in this batch.
        // Binding 0 (mesh) and binding 1 (instances) remain separate command slots.
        if (command is BindVertexBufferCommand binding)
            foreach (
                var existing in _commands
                    .Values.OfType<BindVertexBufferCommand>()
                    .Where(existing =>
                        existing.Drawable.Id == binding.Drawable.Id
                        && existing.Binding == binding.Binding
                        && existing.RenderPassMask == binding.RenderPassMask
                        && existing.Id != binding.Id
                    )
                    .ToArray()
            )
                _commands.Remove(existing.Id);

        var added = _commands.TryAdd(command.Id, command);
        if (!added)
        {
            Debug.WriteLine(
                $"[WARN] Duplicate command ID. CommandType={command.GetType().Name}, PipelineId={command.PipelineId}, "
                    + $"DrawableId={command.Drawable?.Id}, RenderPriority={command.RenderPriority}, CommandId={command.Id}"
            );
        }

        return added;
    }

    /// <inheritdoc />
    public void Remove(DrawableId drawableId)
    {
        foreach (
            var command in _commands
                .Values.Where(command => command.Drawable?.Id == drawableId)
                .ToArray()
        )
            _commands.Remove(command.Id);
    }

    /// <inheritdoc />
    public void RemoveCommand(Guid commandId)
    {
        _commands.Remove(commandId);
    }

    /// <inheritdoc />
    public void Clean()
    {
        foreach (var command in _commands.Values.Where(command => !command.IsSticky).ToArray())
            _commands.Remove(command.Id);
    }
}
