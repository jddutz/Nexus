namespace Nexus.Core;

/// <summary>
/// Identifies a node in a scene hierarchy.
/// </summary>
public readonly record struct SceneNodeId(ulong Value) : IEquatable<SceneNodeId>, IUniqueId
{
    /// <summary>
    /// Converts a numeric value to a <see cref="SceneNodeId"/>.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    public static implicit operator SceneNodeId(ulong value) => new(value);

    /// <summary>
    /// Converts a <see cref="SceneNodeId"/> to its underlying numeric value.
    /// </summary>
    /// <param name="id">The scene node identifier.</param>
    public static implicit operator ulong(SceneNodeId id) => id.Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Creates a new scene node identifier.
    /// </summary>
    /// <returns>A new scene node identifier.</returns>
    public static SceneNodeId New() =>
        new IdentityHashBuilder("SceneNode").Add(Guid.NewGuid()).Compute();

    /// <summary>
    /// Gets an invalid scene node identifier.
    /// </summary>
    public static readonly SceneNodeId Invalid = new(0ul);
}