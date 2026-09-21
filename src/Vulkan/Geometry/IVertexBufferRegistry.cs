namespace Nexus.Graphics.Vulkan.Geometry;

public interface IVertexBufferRegistry : IDisposable
{
    VkBuffer Acquire(IRenderable renderable);

    VkBuffer Get(GraphicsId id);

    void Release(GraphicsId id);

    void Reset();
}
