namespace Nexus.Graphics.Vulkan.Commands;

public interface ICommandFactory
{
    IEnumerable<IVulkanCommand> Create(IDrawable drawable);
}
