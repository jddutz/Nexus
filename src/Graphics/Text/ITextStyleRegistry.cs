namespace Nexus.Graphics.Text;

/// <summary>
/// Builds, caches, and retrieves generated text styles.
/// </summary>
public interface ITextStyleRegistry : IDisposable
{
    /// <summary>
    /// Gets the style for the requested font and size, creating and registering it if needed.
    /// </summary>
    /// <param name="fontId">The content identifier of the font.</param>
    /// <param name="size">The requested text size.</param>
    /// <returns>The cached or newly generated style.</returns>
    ITextStyle GetOrCreate(ContentId fontId, float size);

    /// <summary>
    /// Gets a registered style by its identity.
    /// </summary>
    /// <param name="textstyle">The identifier of the registered style.</param>
    /// <returns>The registered style.</returns>
    /// <exception cref="KeyNotFoundException">The style is not registered.</exception>
    ITextStyle Get(TextStyleId textstyle);

    /// <summary>
    /// Removes all generated styles and atlas textures from the cache.
    /// </summary>
    void Reset();
}
