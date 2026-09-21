namespace Nexus.Graphics.Textures;

/// <summary>
/// Identifies a reusable texture sampling behavior.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the sampling behavior identifier.</param>
public readonly record struct SamplingBehaviorId(ulong Value)
    : IEquatable<SamplingBehaviorId>,
        IUniqueId
{
    /// <summary>
    /// Creates a sampling behavior identifier from an unsigned integer value.
    /// </summary>
    /// <param name="value">The unsigned integer value.</param>
    /// <returns>A sampling behavior identifier containing <paramref name="value"/>.</returns>
    public static implicit operator SamplingBehaviorId(ulong value) => new(value);

    /// <summary>
    /// Converts a sampling behavior identifier to its underlying unsigned integer value.
    /// </summary>
    /// <param name="id">The sampling behavior identifier to convert.</param>
    /// <returns>The unsigned integer value contained by <paramref name="id"/>.</returns>
    public static implicit operator ulong(SamplingBehaviorId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid sampling behavior identifier.
    /// </summary>
    public static readonly SamplingBehaviorId Invalid = new(0ul);
}
