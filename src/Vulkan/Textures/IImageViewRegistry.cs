namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageViewRegistry : IDisposable
{
    ImageView GetOrCreate(ResourceId id, Image image, Format format);

    ImageView Get(ResourceId id);

    void Release(ResourceId id);
}
