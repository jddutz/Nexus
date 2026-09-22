namespace Nexus.Core;

public readonly record struct GameObjectId(ulong Value) : IEquatable<GameObjectId>, IUniqueId
{
    /// <summary>
    /// Creates an GameObjectId from a ulong value.
    /// </summary>
    public static implicit operator GameObjectId(ulong value) => new(value);

    /// <summary>
    /// Converts GameObjectId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(GameObjectId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    public static GameObjectId New() =>
        new IdentityHashBuilder("GameObject").Add(Guid.NewGuid()).Compute();

    public static readonly GameObjectId Invalid = new(0ul);
}
