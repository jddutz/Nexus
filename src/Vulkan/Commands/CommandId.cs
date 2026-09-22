namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Identifies a Vulkan command.
/// </summary>
/// <param name="Value">The underlying unsigned integer value of the identifier.</param>
public readonly record struct CommandId(ulong Value) : IEquatable<CommandId>, IUniqueId
{
    /// <summary>
    /// Converts an unsigned integer value to a command identifier.
    /// </summary>
    /// <param name="value">The value of the identifier.</param>
    /// <returns>A command identifier containing <paramref name="value"/>.</returns>
    public static implicit operator CommandId(ulong value) => new(value);

    /// <summary>
    /// Converts a command identifier to its underlying unsigned integer value.
    /// </summary>
    /// <param name="id">The command identifier to convert.</param>
    /// <returns>The underlying value of <paramref name="id"/>.</returns>
    public static implicit operator ulong(CommandId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <inheritdoc/>
    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;

    /// <summary>
    /// Gets the invalid command identifier.
    /// </summary>
    public static readonly CommandId Invalid = new(0ul);
}
