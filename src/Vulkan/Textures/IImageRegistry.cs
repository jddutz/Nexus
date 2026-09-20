namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageRegistry : IDisposable
{
    Image GetOrCreate(ResourceId id, ITextureDataSource source, ColorFormatEnum format);

    Image Get(ResourceId id);

    void Release(ResourceId id);
}
