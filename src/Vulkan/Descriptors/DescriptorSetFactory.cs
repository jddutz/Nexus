namespace Nexus.Graphics.Vulkan.Descriptors;

public sealed class DescriptorSetFactory(
    Context context,
    IPipelineRegistry pipelineRegistry,
    IImageRegistry imageRegistry,
    ISamplerRegistry samplerRegistry,
    IDescriptorSetPool descriptorSetPool
) : IDescriptorSetFactory
{
    public IReadOnlyList<VkDescriptorSet> Create(PipelineId pipelineId, IDrawable drawable)
    {
        // TODO: Get the schema from somewhere else!
        var schema = DescriptorSchemas.Textured;

        var descriptorSets = new VkDescriptorSet[schema.Sets.Length];

        foreach (var setSchema in schema.Sets)
        {
            var layout = pipelineRegistry.GetDescriptorSetLayout(pipelineId, setSchema.Set);

            var descriptorSet = descriptorSetPool.Allocate(layout);

            foreach (var binding in setSchema.Bindings)
            {
                switch (binding.DescriptorType)
                {
                    case DescriptorType.CombinedImageSampler:
                        // resolve image view + sampler from drawable
                        // descriptorSetPool.WriteCombinedImageSampler(...)
                        break;

                    case DescriptorType.UniformBuffer:
                        // resolve uniform buffer
                        // descriptorSetPool.WriteUniformBuffer(...)
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Descriptor type {binding.DescriptorType} is not supported."
                        );
                }
            }

            descriptorSets[setSchema.Set] = descriptorSet;
        }

        return descriptorSets;
    }
}
