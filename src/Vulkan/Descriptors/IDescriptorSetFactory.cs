namespace Nexus.Graphics.Vulkan.Descriptors;

/// <summary>
/// Creates Vulkan descriptor sets and populates their descriptor bindings.
/// </summary>
public interface IDescriptorSetFactory
{
    IReadOnlyList<VkDescriptorSet> Create(PipelineId pipelineId, IDrawable drawable);
}
