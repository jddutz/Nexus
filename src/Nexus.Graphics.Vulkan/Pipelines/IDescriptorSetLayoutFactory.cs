namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Realizes Vulkan descriptor-set layouts directly from a <see cref="DescriptorSchema"/>,
/// independent of any pipeline. Use this when a component needs a descriptor-set layout that is
/// not tied to a specific pipeline's lifetime (e.g. a shared camera descriptor set).
/// </summary>
public interface IDescriptorSetLayoutFactory
{
    /// <summary>
    /// Creates a descriptor-set layout for each set defined in the schema.
    /// </summary>
    /// <param name="schema">The descriptor schema to realize.</param>
    /// <returns>The created descriptor-set layouts, ordered by set index.</returns>
    DescriptorSetLayout[] Create(DescriptorSchema schema);

    /// <summary>
    /// Destroys previously created descriptor-set layouts.
    /// </summary>
    /// <param name="descriptorSetLayouts">The descriptor-set layouts to destroy.</param>
    void Destroy(DescriptorSetLayout[] descriptorSetLayouts);
}
