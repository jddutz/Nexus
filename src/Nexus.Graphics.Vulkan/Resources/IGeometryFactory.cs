namespace Nexus.Graphics.Vulkan.Resources;

public interface IGeometryFactory
{
    ResourceId Create(UniformColorVertexGeometryDefinition definition);

    ResourceId Read(ResourceId id);

    ResourceId Update(ResourceId id, UniformColorVertexGeometryDefinition definition);

    ResourceId Delete(ResourceId id);
}
