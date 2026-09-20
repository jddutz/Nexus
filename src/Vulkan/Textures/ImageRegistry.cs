namespace Nexus.Graphics.Vulkan.Textures;

public class ImageRegistry : IImageRegistry
{
    private readonly Dictionary<ResourceId, ImageEntry> _images = [];

    private readonly record struct ImageEntry(Image Image, DeviceMemory Memory);

    public VkImage Get(ResourceId id)
    {
        throw new NotImplementedException();
    }

    public VkImage GetOrCreate(ITexture texture, ColorFormatEnum format)
    {
        throw new NotImplementedException();
    }

    public void Release(ResourceId id)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
