namespace Nexus.Graphics.Vulkan.Textures;

public interface ISamplerRegistry : IDisposable
{
    Sampler GetOrCreate(ISamplingBehavior samplingBehavior);

    Sampler Get(GraphicsId id);

    void Release(GraphicsId id);
}
