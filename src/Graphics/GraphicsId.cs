namespace Nexus.Graphics;

/// <summary>
/// Identifies a graphics resource or contribution.
/// </summary>
public readonly record struct GraphicsId(ulong Value) : IEquatable<GraphicsId>, IUniqueId
{
    /// <summary>
    /// Creates a graphics identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator GraphicsId(ulong value) => new(value);

    /// <summary>
    /// Converts a graphics identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(GraphicsId id) => id.Value;

    /// <summary>
    /// Converts an existing component identity to its graphics identity without generating a new value.
    /// </summary>
    public static implicit operator GraphicsId(ComponentId id) => new(id.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid graphics identifier.
    /// </summary>
    public static readonly GraphicsId Invalid = new(0ul);
}
