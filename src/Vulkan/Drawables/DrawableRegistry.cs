namespace Nexus.Graphics.Vulkan.Drawables;

public class DrawableRegistry : IDrawableRegistry
{
    public IEnumerable<IVulkanCommand> Create(IDrawable drawable)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public IEnumerable<IVulkanCommand> Release(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Reset()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Update(IDrawable drawable)
    {
        throw new NotImplementedException();
    }
}
