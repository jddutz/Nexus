namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Creates Vulkan graphics pipelines and their associated pipeline layouts.
/// </summary>
public interface IPipelineFactory
{
    /// <summary>
    /// Creates a graphics pipeline and its associated pipeline layout and descriptor-set layouts.
    /// </summary>
    /// <param name="definition">The definition of the graphics pipeline.</param>
    /// <returns>The created pipeline, its layout, and the descriptor-set layouts realized from the pipeline's descriptor schema.</returns>
    (
        Pipeline Pipeline,
        PipelineLayout Layout,
        DescriptorSetLayout[] DescriptorSetLayouts
    ) Create(PipelineDefinition definition);
}
