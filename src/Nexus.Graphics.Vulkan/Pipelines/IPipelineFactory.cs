namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Creates Vulkan graphics pipelines and their associated pipeline layouts.
/// </summary>
public interface IPipelineFactory
{
    /// <summary>
    /// Creates a graphics pipeline and its associated pipeline layout.
    /// </summary>
    /// <param name="definition">The definition of the graphics pipeline.</param>
    /// <returns>The created pipeline and its associated layout.</returns>
    (Pipeline Pipeline, PipelineLayout Layout) Create(PipelineDefinition definition);
}
