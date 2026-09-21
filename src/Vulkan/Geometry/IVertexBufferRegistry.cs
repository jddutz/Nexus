namespace Nexus.Graphics.Vulkan.Geometry;

public interface IVertexBufferRegistry : IDisposable
{
    VkBuffer Acquire(IRenderable renderable);

    VkBuffer Get(RenderableId id);

    void Release(RenderableId id);

    void Reset();
}
