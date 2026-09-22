namespace Nexus.Graphics.Vulkan.Drawable;

/// <summary>
/// Creates Vulkan commands for drawable resources and manages their lifetime.
/// </summary>
public class DrawableRegistry : IDrawableRegistry
{
    private readonly Dictionary<DrawableId, IVulkanCommand> _drawCommands = [];

    public IEnumerable<IVulkanCommand> Create(IDrawable drawable)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Update(IDrawable drawable)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IVulkanCommand> Release(IDrawable drawable)
    {
        throw new NotImplementedException();
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
