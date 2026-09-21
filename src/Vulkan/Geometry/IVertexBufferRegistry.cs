namespace Nexus.Graphics.Vulkan.Geometry;

public interface IVertexBufferRegistry : IDisposable
{
    VkBuffer GetOrCreate(IVertexDataSource source, VertexFormat format);

    VkBuffer Get(GraphicsId id);

    void Release(GraphicsId id);
}
