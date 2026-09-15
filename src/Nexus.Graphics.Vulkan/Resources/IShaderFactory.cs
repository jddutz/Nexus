namespace Nexus.Graphics.Vulkan.Resources;

public interface IShaderFactory
{
    ResourceId Create(ShaderDefinition definition);

    ShaderModule Read(ResourceId id);

    void Update(ResourceId id, ShaderDefinition definition);

    void Delete(ResourceId id);
}
