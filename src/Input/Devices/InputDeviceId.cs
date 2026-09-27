namespace Nexus.Input;

/// <summary>
/// Identifies an input device.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the input device identifier. Zero represents an invalid identifier.</param>
public readonly record struct InputDeviceId(ulong Value) : IEquatable<InputDeviceId>, IUniqueId
{
    /// <summary>
    /// Creates an input device identifier from an unsigned integer value.
    /// </summary>
    /// <param name="value">The unsigned integer value of the identifier.</param>
    /// <returns>The input device identifier represented by <paramref name="value"/>.</returns>
    public static implicit operator InputDeviceId(ulong value) => new(value);

    /// <summary>
    /// Converts an input device identifier to its underlying unsigned integer value.
    /// </summary>
    /// <param name="id">The input device identifier to convert.</param>
    /// <returns>The unsigned integer value of <paramref name="id"/>.</returns>
    public static implicit operator ulong(InputDeviceId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Determines whether this identifier has the same value as another unique identifier.
    /// </summary>
    /// <param name="other">The unique identifier to compare with this identifier.</param>
    /// <returns><see langword="true"/> if both identifiers have the same value; otherwise, <see langword="false"/>.</returns>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid input device identifier, whose value is zero.
    /// </summary>
    public static readonly InputDeviceId Invalid = new(0ul);
}
