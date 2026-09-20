namespace Nexus.Graphics.Vulkan.Geometry;

public interface IVertexBufferRegistry : IDisposable
{
    VkBuffer GetOrCreate(ResourceId id, IVertexDataSource source, VertexFormat format);

    VkBuffer Get(ResourceId id);

    void Release(ResourceId id);
}
