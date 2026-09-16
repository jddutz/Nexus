using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan.Components;

public interface IGeometryFactory
{
    ResourceId Create(UniformColorVertexGeometryDefinition definition);

    ResourceId Create(TexturedVertex2dGeometryDefinition definition);

    ResourceId Read(ResourceId id);

    ResourceId Update(ResourceId id, UniformColorVertexGeometryDefinition definition);

    ResourceId Delete(ResourceId id);

    VkBuffer ReadBuffer(ResourceId id);

    uint ReadVertexCount(ResourceId id);
}
