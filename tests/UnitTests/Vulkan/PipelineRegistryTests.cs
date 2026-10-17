using Nexus.Graphics.Vulkan;
using Nexus.Graphics.Vulkan.Pipelines;
using Silk.NET.Vulkan;

namespace Tests;

/// <summary>Checks pipeline reuse without creating a Vulkan device.</summary>
public sealed class PipelineRegistryTests
{
    /// <summary>Scene teardown must leave pipelines and descriptor layouts available to the next scene.</summary>
    [Fact]
    public void Released_pipeline_is_reused_across_scene_transitions()
    {
        var factory = new CountingFactory();
        // No native operations are expected during acquisition or release.
        var registry = new PipelineRegistry(null!, factory);
        var definition = new PipelineDefinition("retained", null, null, null, null, null, default);

        var original = registry.GetOrCreate(definition);
        registry.GetOrCreate(definition);
        registry.Release(definition.Id);
        registry.Release(definition.Id);
        registry.Release(definition.Id); // Repeated release must not evict the cached entry.

        Assert.Equal(original.pipeline, registry.Get(definition.Id));
        Assert.Equal(original.layout, registry.GetLayout(definition.Id));
        Assert.Equal(1, registry.GetDescriptorSetLayoutCount(definition.Id));
        Assert.Equal(3UL, registry.GetDescriptorSetLayout(definition.Id, 0).Handle);
        Assert.Equal(original, registry.GetOrCreate(definition));
        registry.Release(definition.Id);
        Assert.Equal(original, registry.GetOrCreate(definition));
        Assert.Equal(1, factory.Creations);
    }

    private sealed class CountingFactory : IPipelineFactory
    {
        public int Creations { get; private set; }

        public (Pipeline Pipeline, PipelineLayout Layout, DescriptorSetLayout[] DescriptorSetLayouts)
            Create(PipelineDefinition definition)
        {
            Creations++;
            return (new Pipeline { Handle = 1 }, new PipelineLayout { Handle = 2 },
                [new DescriptorSetLayout { Handle = 3 }]);
        }
    }
}
