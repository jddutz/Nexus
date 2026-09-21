namespace Nexus.Graphics.Textures;

/// <summary>
/// Identifies a texture resource.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the texture identifier.</param>
public readonly record struct TextureId(ulong Value) : IEquatable<TextureId>, IUniqueId
{
    /// <summary>
    /// Creates a texture identifier from an unsigned integer value.
    /// </summary>
    public static implicit operator TextureId(ulong value) => new(value);

    /// <summary>
    /// Converts a texture identifier to its underlying unsigned integer value.
    /// </summary>
    public static implicit operator ulong(TextureId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid texture identifier.
    /// </summary>
    public static readonly TextureId Invalid = new(0ul);
}
