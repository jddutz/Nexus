namespace Nexus.Graphics.Vulkan.Drawables;

public interface IDrawableRegistry : IDisposable
{
    IEnumerable<IVulkanCommand> Create(IDrawable drawable);
    IEnumerable<IVulkanCommand> Update(IDrawable drawable);
    IEnumerable<IVulkanCommand> Release(IDrawable drawable);
    IEnumerable<IVulkanCommand> Reset();
}
