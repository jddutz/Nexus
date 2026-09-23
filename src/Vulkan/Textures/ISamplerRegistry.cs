namespace Nexus.Graphics.Vulkan.Textures;

public interface ISamplerRegistry : IDisposable
{
    void Create(ISamplingBehavior behavior);

    VkSampler Get(SamplingBehaviorId id);

    void Release(ISamplingBehavior behavior);

    void Reset();
}
