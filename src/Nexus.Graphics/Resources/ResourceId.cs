namespace Nexus.Graphics.Resources;

public readonly record struct ResourceId(ulong Value) : IEquatable<ResourceId>, IUniqueId
{
    /// <summary>
    /// Creates an ResourceId from a ulong value.
    /// </summary>
    public static implicit operator ResourceId(ulong value) => new(value);

    /// <summary>
    /// Converts ResourceId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(ResourceId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;
}
