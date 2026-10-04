namespace Nexus.Graphics.Textures;

/// <summary>
/// Caches loaded textures for the lifetime of the registry.
/// </summary>
public interface ITextureRegistry : IDisposable
{
    /// <summary>
    /// Gets an existing texture for content or loads and registers it.
    /// </summary>
    /// <param name="texture">The content identifier of the texture.</param>
    /// <returns>The loaded or already registered texture.</returns>
    ITexture GetOrCreate(ContentId texture);

    /// <summary>
    /// Gets a registered texture by its identity.
    /// </summary>
    /// <param name="texture">The identifier of the registered texture.</param>
    /// <returns>The registered texture.</returns>
    ITexture Get(TextureId texture);

    /// <summary>
    /// Removes all registered textures from the cache.
    /// </summary>
    void Reset();
}
