namespace Nexus.Graphics.Vulkan.Geometry;

public interface IVertexBufferRegistry : IDisposable
{
    VkBuffer GetOrCreate(GraphicsId id, IVertexDataSource source, VertexFormat format);

    VkBuffer Get(GraphicsId id);

    void Release(GraphicsId id);
}
