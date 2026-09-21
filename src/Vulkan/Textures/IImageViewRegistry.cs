namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageViewRegistry : IDisposable
{
    ImageView Acquire(DrawableId imageId, Image image, Format format);

    ImageView Get(DrawableId id);

    void Release(DrawableId id);

    void Reset();
}
