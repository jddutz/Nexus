namespace Nexus.Graphics.Vulkan.Components;

public interface IShaderFactory
{
    GraphicsId Create(IShaderContract description);

    ShaderModule Read(GraphicsId id);

    void Update(GraphicsId id, IShaderContract description);

    void Delete(GraphicsId id);
}
