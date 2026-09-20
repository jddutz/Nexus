namespace Nexus.Graphics.Vulkan.Textures;

public class ImageRegistry : IImageRegistry
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Image Get(ResourceId id)
    {
        throw new NotImplementedException();
    }

    public Image GetOrCreate(ResourceId id, ITextureDataSource source, ColorFormatEnum format)
    {
        throw new NotImplementedException();
    }

    public void Release(ResourceId id)
    {
        throw new NotImplementedException();
    }
}
