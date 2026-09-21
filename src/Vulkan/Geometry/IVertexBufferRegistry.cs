namespace Nexus.Graphics.Vulkan.Geometry;

public interface IVertexBufferRegistry : IDisposable
{
    VkBuffer Acquire(IRenderable renderable);

    VkBuffer Get(MeshId meshId, VertexFormatId formatId);

    void Release(MeshId id);

    void Reset();
}
