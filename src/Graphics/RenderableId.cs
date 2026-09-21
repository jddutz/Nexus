namespace Nexus.Graphics;

/// <summary>
/// Identifies a renderable graphics contribution.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the identifier.</param>
public readonly record struct DrawableId(ulong Value) : IEquatable<DrawableId>, IUniqueId
{
    /// <summary>
    /// Creates a graphics identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator DrawableId(ulong value) => new(value);

    /// <summary>
    /// Converts a graphics identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(DrawableId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid graphics identifier.
    /// </summary>
    public static readonly DrawableId Invalid = new(0ul);
}
