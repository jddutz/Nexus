namespace Nexus.Graphics.Geometry;

/// <summary>
/// Identifies a reusable vertex format.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the vertex format identifier.</param>
public readonly record struct VertexFormatId(ulong Value) : IEquatable<VertexFormatId>, IUniqueId
{
    /// <summary>
    /// Creates a vertex format identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator VertexFormatId(ulong value) => new(value);

    /// <summary>
    /// Converts a vertex format identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(VertexFormatId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid vertex format identifier.
    /// </summary>
    public static readonly VertexFormatId Invalid = new(0ul);
}
