namespace Nexus.Graphics.Textures;

public interface ITextureRegistry : IDisposable
{
    ITexture GetOrCreate(ContentId texture);
    ITexture Get(TextureId texture);
    IEnumerable<ITexture> Update(TextureId texture);
    IEnumerable<ITexture> Release(TextureId texture);

    void Reset();
}
