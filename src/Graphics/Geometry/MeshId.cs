namespace Nexus.Graphics.Geometry;

/// <summary>
/// Identifies a reusable mesh geometry instance.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the mesh identifier.</param>
public readonly record struct MeshId(ulong Value) : IEquatable<MeshId>, IUniqueId
{
    /// <summary>
    /// Creates a mesh identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator MeshId(ulong value) => new(value);

    /// <summary>
    /// Converts a mesh identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(MeshId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid mesh identifier.
    /// </summary>
    public static readonly MeshId Invalid = new(0ul);
}
