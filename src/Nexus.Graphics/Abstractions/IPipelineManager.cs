namespace Nexus.Graphics.Abstractions;

/// <summary>
/// Manages graphics pipelines with caching, lifecycle management, and hot-reload support.
/// Thread-safe pipeline creation and access for multi-threaded loading scenarios.
/// </summary>
/// <remarks>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item>Pipeline creation with descriptor-based caching</item>
/// <item>Pipeline invalidation when resources change</item>
/// <item>Shader hot-reload for development workflow</item>
/// <item>Thread-safe concurrent pipeline access</item>
/// <item>Graceful degradation with fallback pipelines</item>
/// </list>
///
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// var pipeline = pipelineManager.GetOrCreate(PipelineDefinitions.UIElement);
/// </code>
/// </remarks>
public interface IPipelineManager : IDisposable
{
    /// <summary>
    /// Gets or creates a pipeline using a pipeline definition.
    /// If a pipeline with the definition's name exists in cache, returns it.
    /// Otherwise, builds a new pipeline using the definition's configuration and caches it.
    /// This is the preferred method for creating pipelines from static definitions.
    /// </summary>
    /// <param name="definition">Pipeline definition containing name and configuration.</param>
    /// <returns>Pipeline handle, ready for use in draw commands.</returns>
    /// <exception cref="ArgumentNullException">If definition is null.</exception>
    /// <remarks>
    /// Use with static pipeline definitions for automatic caching and reuse:
    /// <code>
    /// var pipeline = pipelineManager.GetOrCreate(PipelineDefinitions.UIElement);
    /// </code>
    /// All components using the same definition will share the same pipeline instance.
    /// </remarks>
    PipelineHandle GetOrCreate(PipelineDefinition definition);

    /// <summary>
    /// Creates a new pipeline builder for fluent pipeline configuration.
    /// </summary>
    /// <returns>A new pipeline builder instance.</returns>
    /// <remarks>
    /// Use the builder to configure a pipeline fluently:
    /// <code>
    /// var pipeline = pipelineManager.GetBuilder()
    ///     .WithShader(shader)
    ///     .WithTopology(PrimitiveTopology.TriangleFan)
    ///     .Build("MyPipeline");
    /// </code>
    /// </remarks>
    IPipelineBuilder GetBuilder();

    /// <summary>
    /// Retrieves a handle for the specified pipeline from the cache
    /// or throws <see cref="InvalidOperationException"/> if it does not exist.
    /// </summary>
    /// <param name="name">The name of the pipeline to be retrieved from the cache.</param>
    /// <returns>The specified pipeline handle.</returns>
    PipelineHandle Get(string name);

    /// <summary>
    /// Invalidates and removes a specific pipeline from the cache.
    /// Pipeline will be recreated on next access.
    /// Thread-safe - safe to call during rendering.
    /// </summary>
    /// <param name="pipelineName">Name of the pipeline to invalidate</param>
    /// <returns>True if pipeline was found and invalidated, false if not found</returns>
    /// <remarks>
    /// Use this when you know a specific pipeline needs to be rebuilt,
    /// such as when its shader files are modified during development.
    /// </remarks>
    bool InvalidatePipeline(string pipelineName);

    /// <summary>
    /// Invalidates all pipelines using the specified shader.
    /// Useful for shader hot-reload scenarios.
    /// Thread-safe - safe to call during rendering.
    /// </summary>
    /// <param name="shaderPath">Path to the shader file that changed</param>
    /// <returns>Number of pipelines invalidated</returns>
    /// <remarks>
    /// When a shader file changes on disk, this method finds all pipelines
    /// that reference it and marks them for recreation. Pipelines are lazily
    /// rebuilt on next access.
    /// </remarks>
    int InvalidatePipelinesUsingShader(string shaderPath);

    /// <summary>
    /// Reloads all shader files and recreates affected pipelines.
    /// Development feature for hot-reload workflow.
    /// Blocking operation - waits for the GPU to go idle before destroying pipelines.
    /// </summary>
    /// <remarks>
    /// This is a heavy operation and should only be used during development.
    /// For production, use InvalidatePipelinesUsingShader() for targeted updates.
    /// </remarks>
    void ReloadAllShaders();

    /// <summary>
    /// Gets statistics about pipeline usage and cache performance.
    /// Useful for debugging and profiling.
    /// </summary>
    /// <returns>Statistics including cache hits, misses, and active pipeline count</returns>
    PipelineStatistics GetStatistics();
}
