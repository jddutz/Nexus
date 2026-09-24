namespace Nexus.Core;

public readonly record struct ComponentId(ulong Value) : IEquatable<ComponentId>, IUniqueId
{
    /// <summary>
    /// Creates an ComponentId from a ulong value.
    /// </summary>
    public static implicit operator ComponentId(ulong value) => new(value);

    /// <summary>
    /// Converts ComponentId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(ComponentId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    private static long _nextId;

    public static ComponentId New() => new((ulong)Interlocked.Increment(ref _nextId));

    public static readonly ComponentId Invalid = new(0ul);
}
