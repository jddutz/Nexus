namespace Nexus.Graphics.Vulkan;

public interface IShaderFactory
{
    RenderableId Create(IShaderContract description);

    ShaderModule Read(RenderableId id);

    void Update(RenderableId id, IShaderContract description);

    void Delete(RenderableId id);
}
