namespace Nexus.Graphics.Vulkan.Textures;

public interface ISamplerRegistry : IDisposable
{
    Sampler Acquire(ISamplingBehavior samplingBehavior);

    Sampler Get(GraphicsId id);

    void Release(GraphicsId id);

    void Reset();
}
