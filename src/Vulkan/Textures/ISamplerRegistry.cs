namespace Nexus.Graphics.Vulkan.Textures;

public interface ISamplerRegistry : IDisposable
{
    Sampler Acquire(ISamplingBehavior samplingBehavior);

    Sampler Get(RenderableId id);

    void Release(RenderableId id);

    void Reset();
}
