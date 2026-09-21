namespace Nexus.Graphics.Vulkan.Textures;

public interface ISamplerRegistry : IDisposable
{
    Sampler Acquire(ISamplingBehavior samplingBehavior);

    Sampler Get(DrawableId id);

    void Release(DrawableId id);

    void Reset();
}
