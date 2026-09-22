namespace Nexus.Game;

/// <summary>
/// Identifies a scene.
/// </summary>
public readonly record struct SceneId(ulong Value) : IEquatable<SceneId>, IUniqueId
{
    /// <summary>
    /// Converts a <see cref="ulong"/> to a <see cref="SceneId"/>.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    public static implicit operator SceneId(ulong value) => new(value);

    /// <summary>
    /// Converts a <see cref="SceneId"/> to its underlying value.
    /// </summary>
    /// <param name="id">The scene identifier.</param>
    public static implicit operator ulong(SceneId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other is not null && Value == other.Value;

    /// <summary>
    /// Creates a new scene identifier.
    /// </summary>
    /// <returns>A new scene identifier.</returns>
    public static SceneId New() =>
        new IdentityHashBuilder(nameof(Scene)).Add(Guid.NewGuid()).Compute();

    /// <summary>
    /// Gets an invalid scene identifier.
    /// </summary>
    public static readonly SceneId Invalid = new(0ul);
}