namespace Nexus.Graphics.Text;

/// <summary>
/// Builds, caches, and retrieves generated text styles.
/// </summary>
public interface ITextStyleRegistry : IDisposable
{
    /// <summary>
    /// Gets an existing style for the description or builds and registers it.
    /// </summary>
    /// <param name="description">The font and size configuration of the style.</param>
    /// <returns>The cached or newly generated style.</returns>
    ITextStyle GetOrCreate(TextStyleDescription description);

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
