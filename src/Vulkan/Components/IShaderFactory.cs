namespace Nexus.Graphics.Vulkan.Components;

public interface IShaderFactory
{
    RenderableId Create(IShaderContract description);

    ShaderModule Read(RenderableId id);

    void Update(RenderableId id, IShaderContract description);

    void Delete(RenderableId id);
}
