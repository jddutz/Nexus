namespace Nexus.Graphics.Vulkan.Textures;

public interface ISamplerRegistry : IDisposable
{
    Sampler GetOrCreate(ISamplingBehavior samplingBehavior);

    Sampler Get(ResourceId id);

    void Release(ResourceId id);
}
