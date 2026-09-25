namespace Nexus.Graphics;

/// <summary>
/// Uniquely identifies a graphics window.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the window identifier. Zero represents an invalid window identifier.</param>
public readonly record struct WindowId(ulong Value) : IEquatable<WindowId>, IUniqueId
{
    /// <summary>
    /// Creates a window identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator WindowId(ulong value) => new(value);

    /// <summary>
    /// Converts a window identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(WindowId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid window identifier, represented by a value of zero.
    /// </summary>
    public static readonly WindowId Invalid = new(0ul);
}
