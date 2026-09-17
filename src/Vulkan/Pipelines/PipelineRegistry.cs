namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// A realized Vulkan pipeline together with the resources owned by its registry entry.
/// </summary>
/// <param name="Pipeline">The graphics pipeline.</param>
/// <param name="Layout">The pipeline layout.</param>
/// <param name="DescriptorSetLayouts">The descriptor-set layouts realized from the pipeline's descriptor schema.</param>
internal sealed record PipelineEntry(
    Pipeline Pipeline,
    PipelineLayout Layout,
    DescriptorSetLayout[] DescriptorSetLayouts
);

/// <summary>
/// Provides identifier-based access to Vulkan graphics pipelines and layouts.
/// </summary>
public unsafe class PipelineRegistry(Context context, IPipelineFactory pipelineFactory)
    : IPipelineRegistry
{
    private readonly Context _context = context;
    private readonly IPipelineFactory _pipelineFactory = pipelineFactory;

    private readonly Dictionary<PipelineId, PipelineEntry> _pipelines = [];

    /// <inheritdoc />
    public (Pipeline pipeline, PipelineLayout layout) GetOrCreate(PipelineDefinition description)
    {
        if (_pipelines.TryGetValue(description.Id, out var existing))
            return (existing.Pipeline, existing.Layout);

        var (pipeline, layout, descriptorSetLayouts) = _pipelineFactory.Create(description);
        var entry = new PipelineEntry(pipeline, layout, descriptorSetLayouts);

        _pipelines.Add(description.Id, entry);
        return (entry.Pipeline, entry.Layout);
    }

    /// <inheritdoc />
    public Pipeline Get(PipelineId id)
    {
        if (!_pipelines.TryGetValue(id, out var record))
            throw new KeyNotFoundException($"Pipeline with ID {id} was not found.");

        return record.Pipeline;
    }

    /// <inheritdoc />
    public PipelineLayout GetLayout(PipelineId id)
    {
        if (!_pipelines.TryGetValue(id, out var record))
            throw new KeyNotFoundException($"Pipeline with ID {id} was not found.");

        return record.Layout;
    }

    /// <inheritdoc />
    public DescriptorSetLayout GetDescriptorSetLayout(PipelineId pipelineId, uint set)
    {
        if (!_pipelines.TryGetValue(pipelineId, out var record))
            throw new KeyNotFoundException($"Pipeline with ID {pipelineId} was not found.");

        if (set >= record.DescriptorSetLayouts.Length)
            throw new ArgumentOutOfRangeException(
                nameof(set),
                $"Pipeline with ID {pipelineId} has no descriptor set at index {set}."
            );

        return record.DescriptorSetLayouts[set];
    }

    /// <inheritdoc />
    public int GetDescriptorSetLayoutCount(PipelineId pipelineId)
    {
        if (!_pipelines.TryGetValue(pipelineId, out var record))
            throw new KeyNotFoundException($"Pipeline with ID {pipelineId} was not found.");

        return record.DescriptorSetLayouts.Length;
    }

    /// <inheritdoc />
    public void Release(PipelineId id)
    {
        if (_pipelines.Remove(id, out var entry))
        {
            _context.VulkanApi.DestroyPipeline(_context.Device, entry.Pipeline, null);
            _context.VulkanApi.DestroyPipelineLayout(_context.Device, entry.Layout, null);

            foreach (var descriptorSetLayout in entry.DescriptorSetLayouts)
                _context.VulkanApi.DestroyDescriptorSetLayout(
                    _context.Device,
                    descriptorSetLayout,
                    null
                );
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var entry in _pipelines.Values)
        {
            _context.VulkanApi.DestroyPipeline(_context.Device, entry.Pipeline, null);
            _context.VulkanApi.DestroyPipelineLayout(_context.Device, entry.Layout, null);

            foreach (var descriptorSetLayout in entry.DescriptorSetLayouts)
                _context.VulkanApi.DestroyDescriptorSetLayout(
                    _context.Device,
                    descriptorSetLayout,
                    null
                );
        }

        _pipelines.Clear();
        GC.SuppressFinalize(this);
    }
}
