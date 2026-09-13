namespace Nexus.GameModel;

public readonly record struct SceneId(ulong Value) : IEquatable<SceneId>, IUniqueId
{
    /// <summary>
    /// Creates an SceneId from a ulong value.
    /// </summary>
    public static implicit operator SceneId(ulong value) => new(value);

    /// <summary>
    /// Converts SceneId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(SceneId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;
}
