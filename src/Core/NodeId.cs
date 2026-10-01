namespace Nexus.Core;

/// <summary>
/// Identifies a node in a scene hierarchy.
/// </summary>
public readonly record struct NodeId(ulong Value) : IEquatable<NodeId>, IUniqueId
{
    /// <summary>
    /// Converts a numeric value to a <see cref="NodeId"/>.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    public static implicit operator NodeId(ulong value) => new(value);

    /// <summary>
    /// Converts a <see cref="NodeId"/> to its underlying numeric value.
    /// </summary>
    /// <param name="id">The scene node identifier.</param>
    public static implicit operator ulong(NodeId id) => id.Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Creates a new scene node identifier.
    /// </summary>
    /// <returns>A new scene node identifier.</returns>
    public static NodeId New() =>
        new IdentityHashBuilder("SceneNode").Add(Guid.NewGuid()).Compute();

    /// <summary>
    /// Gets an invalid scene node identifier.
    /// </summary>
    public static readonly NodeId Invalid = new(0ul);
}
