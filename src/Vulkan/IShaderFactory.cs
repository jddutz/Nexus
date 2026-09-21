namespace Nexus.Graphics.Vulkan;

public interface IShaderFactory
{
    DrawableId Create(IShaderContract description);

    ShaderModule Read(DrawableId id);

    void Update(DrawableId id, IShaderContract description);

    void Delete(DrawableId id);
}
