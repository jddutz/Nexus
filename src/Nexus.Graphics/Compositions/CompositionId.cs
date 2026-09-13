namespace Nexus.Graphics.Compositions;

public readonly record struct CompositionId(ulong Value) : IEquatable<CompositionId>, IUniqueId
{
    /// <summary>
    /// Creates an CompositionId from a ulong value.
    /// </summary>
    public static implicit operator CompositionId(ulong value) => new(value);

    /// <summary>
    /// Converts CompositionId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(CompositionId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;
}
