namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Provides identifier-based access to Vulkan graphics pipelines and layouts.
/// </summary>
public unsafe class PipelineRegistry(Context context, IPipelineFactory pipelineFactory)
    : IPipelineRegistry
{
    private readonly Context _context = context;
    private readonly IPipelineFactory _pipelineFactory = pipelineFactory;

    private readonly Dictionary<
        PipelineId,
        (Pipeline Pipeline, PipelineLayout Layout)
    > _pipelines = [];

    /// <inheritdoc />
    public (Pipeline pipeline, PipelineLayout layout) GetOrCreate(PipelineDefinition description)
    {
        if (_pipelines.TryGetValue(description.Id, out var existing))
            return existing;

        var resources = _pipelineFactory.Create(description);

        _pipelines.Add(description.Id, resources);
        return resources;
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
    public void Release(PipelineId id)
    {
        if (_pipelines.Remove(id, out var resources))
        {
            _context.VulkanApi.DestroyPipeline(_context.Device, resources.Pipeline, null);
            _context.VulkanApi.DestroyPipelineLayout(_context.Device, resources.Layout, null);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (pipeline, layout) in _pipelines.Values)
        {
            _context.VulkanApi.DestroyPipeline(_context.Device, pipeline, null);
            _context.VulkanApi.DestroyPipelineLayout(_context.Device, layout, null);
        }

        _pipelines.Clear();
        GC.SuppressFinalize(this);
    }
}
