namespace Nexus.Graphics.Vulkan.Components;

public interface IShaderFactory
{
    ResourceId Create(Shader description);

    ShaderModule Read(ResourceId id);

    void Update(ResourceId id, Shader description);

    void Delete(ResourceId id);
}
