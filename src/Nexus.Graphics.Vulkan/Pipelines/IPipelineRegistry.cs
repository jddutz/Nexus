namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Manages the lifetime of Vulkan graphics pipelines.
/// </summary>
public interface IPipelineRegistry : IDisposable
{
    /// <summary>
    /// Gets the pipeline and layout for the supplied description, creating them when absent.
    /// </summary>
    /// <param name="description">The pipeline configuration.</param>
    /// <returns>The pipeline and its associated layout.</returns>
    /// <remarks>
    /// The PipelineId is based on the PipelineDefinition,
    /// so reusing the same PipelineDefinition will not create a new
    /// pipeline, it will return the existing one from the registry.
    /// </remarks>
    (Pipeline pipeline, PipelineLayout layout) GetOrCreate(PipelineDefinition description);

    /// <summary>
    /// Gets a pipeline by its unique identifier.
    /// </summary>
    /// <param name="id">The pipeline identifier.</param>
    /// <returns>The specified pipeline, if registered.</returns>
    /// <exception cref="KeyNotFoundException">The identifier is not registered.</exception>
    Pipeline Get(PipelineId id);

    /// <summary>
    /// Gets a pipeline layout using the pipeline identifier as a key.
    /// </summary>
    /// <param name="id">The pipeline identifier.</param>
    /// <returns>The specified pipeline layout, if registered.</returns>
    /// <exception cref="KeyNotFoundException">The identifier is not registered.</exception>
    PipelineLayout GetLayout(PipelineId id);

    /// <summary>
    /// Deletes a pipeline and layout and releases Vulkan resources.
    /// </summary>
    /// <param name="id">The pipeline identifier.</param>
    void Release(PipelineId id);
}
