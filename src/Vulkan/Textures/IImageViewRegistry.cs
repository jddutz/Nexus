namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageViewRegistry : IDisposable
{
    ImageView Acquire(GraphicsId imageId, Image image, Format format);

    ImageView Get(GraphicsId id);

    void Release(GraphicsId id);

    void Reset();
}
