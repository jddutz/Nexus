namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Manages the lifetime of Vulkan graphics pipelines.
/// </summary>
public interface IPipelineManager : IDisposable
{
    /// <summary>
    /// Creates a pipeline from the supplied description.
    /// </summary>
    /// <param name="description">The pipeline configuration.</param>
    /// <returns>The identifier of the created pipeline.</returns>
    ulong Create(PipelineDefinition description);

    /// <summary>
    /// Gets a pipeline record by identifier.
    /// </summary>
    /// <param name="id">The pipeline identifier.</param>
    /// <returns>The pipeline record.</returns>
    /// <exception cref="KeyNotFoundException">The identifier is not registered.</exception>
    PipelineRecord Get(ulong id);

    /// <summary>
    /// Deletes a pipeline and releases its Vulkan resources.
    /// </summary>
    /// <param name="id">The pipeline identifier.</param>
    void Delete(ulong id);
}
