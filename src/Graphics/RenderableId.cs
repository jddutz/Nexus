namespace Nexus.Graphics;

public readonly record struct RenderableId(ulong Value) : IEquatable<RenderableId>, IUniqueId
{
    /// <summary>
    /// Creates an RenderableId from a ulong value.
    /// </summary>
    public static implicit operator RenderableId(ulong value) => new(value);

    /// <summary>
    /// Converts RenderableId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(RenderableId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    public static readonly RenderableId Invalid = new(0ul);
}
