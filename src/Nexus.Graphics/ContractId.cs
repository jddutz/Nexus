namespace Nexus.Graphics;

public readonly record struct ContractId(ulong Value) : IEquatable<ContractId>, IUniqueId
{
    /// <summary>
    /// Creates an ContractId from a ulong value.
    /// </summary>
    public static implicit operator ContractId(ulong value) => new(value);

    /// <summary>
    /// Converts ContractId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(ContractId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;
}
