namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageViewRegistry : IDisposable
{
    ImageView GetOrCreate(GraphicsId id, Image image, Format format);

    ImageView Get(GraphicsId id);

    void Release(GraphicsId id);
}
