namespace Nexus.Graphics.Vulkan.Textures;

public interface IImageViewRegistry : IDisposable
{
    ImageView Acquire(RenderableId imageId, Image image, Format format);

    ImageView Get(RenderableId id);

    void Release(RenderableId id);

    void Reset();
}
