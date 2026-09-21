namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Identifies a render item within the Vulkan renderer.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the identifier.</param>
public readonly record struct RenderItemId(ulong Value) : IEquatable<RenderItemId>, IUniqueId
{
    /// <summary>
    /// Converts an unsigned integer value to a render item identifier.
    /// </summary>
    /// <param name="value">The value of the identifier.</param>
    /// <returns>A render item identifier containing <paramref name="value"/>.</returns>
    public static implicit operator RenderItemId(ulong value) => new(value);

    /// <summary>
    /// Converts a render item identifier to its underlying unsigned integer value.
    /// </summary>
    /// <param name="id">The render item identifier to convert.</param>
    /// <returns>The underlying value of <paramref name="id"/>.</returns>
    public static implicit operator ulong(RenderItemId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid render item identifier.
    /// </summary>
    public static readonly RenderItemId Invalid = new(0ul);
}
