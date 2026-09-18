namespace Nexus.Core;

/// <summary>
/// Provides content instances of type <typeparamref name="TSource"/>, resolved by <see cref="ContentId"/>.
/// </summary>
/// <typeparam name="TSource">The type of content this provider resolves.</typeparam>
public interface IContentProvider<TSource>
{
    /// <summary>
    /// Gets the content instance identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The identifier of the content to retrieve.</param>
    /// <returns>The resolved content instance.</returns>
    TSource Get(ContentId id);
}
