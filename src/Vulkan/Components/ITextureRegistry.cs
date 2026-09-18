namespace Nexus.Graphics.Vulkan.Components;

public interface ITextureRegistry
{
    bool IsRegistered(ResourceId id);
    void Register(
        Texture description,
        ITextureSource source,
        ColorFormatEnum format = ColorFormatEnum.RGBA8UNorm
    );
    bool TryGetImageView(ResourceId id, out ImageView imageView);
    bool TryGetSampler(ResourceId id, out Sampler sampler);
    void Remove(ResourceId id);
}
