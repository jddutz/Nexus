namespace Nexus.Graphics.Text;

/// <summary>
/// Identifies a textstyle resource.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the textstyle identifier.</param>
public readonly record struct TextStyleId(ulong Value) : IEquatable<TextStyleId>, IUniqueId
{
    /// <summary>
    /// Creates a textstyle identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator TextStyleId(ulong value) => new(value);

    /// <summary>
    /// Converts a textstyle identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(TextStyleId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid textstyle identifier.
    /// </summary>
    public static readonly TextStyleId Invalid = new(0ul);
}
