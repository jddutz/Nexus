namespace Nexus.Graphics.Vulkan.Components;

public interface ITextureRegistry
{
    bool IsRegistered(ContentId id);
    void Register(
        Texture description,
        ITexture source,
        ColorFormatEnum format = ColorFormatEnum.RGBA8UNorm
    );
    bool TryGetImageView(ContentId id, out ImageView imageView);
    bool TryGetSampler(ContentId id, out Sampler sampler);
    void Remove(ContentId id);
}
