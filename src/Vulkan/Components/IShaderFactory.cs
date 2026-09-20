namespace Nexus.Graphics.Vulkan.Components;

public interface IShaderFactory
{
    ResourceId Create(IShaderContract description);

    ShaderModule Read(ResourceId id);

    void Update(ResourceId id, IShaderContract description);

    void Delete(ResourceId id);
}
