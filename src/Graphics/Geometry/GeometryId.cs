namespace Nexus.Graphics.Geometry;

/// <summary>
/// Identifies a reusable geometry instance.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the geometry identifier.</param>
public readonly record struct GeometryId(ulong Value) : IEquatable<GeometryId>, IUniqueId
{
    /// <summary>
    /// Creates a geometry identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator GeometryId(ulong value) => new(value);

    /// <summary>
    /// Converts a geometry identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(GeometryId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid geometry identifier.
    /// </summary>
    public static readonly GeometryId Invalid = new(0ul);
}
