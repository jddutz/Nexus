namespace Nexus.Graphics.Vulkan.Components;

public interface IShaderFactory
{
    ResourceId Create(ShaderDescription description);

    ShaderModule Read(ResourceId id);

    void Update(ResourceId id, ShaderDescription description);

    void Delete(ResourceId id);
}
