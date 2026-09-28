namespace Nexus.Game;

/// <summary>
/// Identifies a scene.
/// </summary>
public readonly record struct SceneId(string Value) : IEquatable<SceneId>
{
    /// <summary>
    /// Converts a string to a <see cref="SceneId"/>.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    public static implicit operator SceneId(string value) => new(value);

    /// <summary>
    /// Converts a <see cref="SceneId"/> to its underlying value.
    /// </summary>
    /// <param name="id">The scene identifier.</param>
    public static implicit operator string(SceneId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Creates a new scene identifier.
    /// </summary>
    /// <returns>A new scene identifier.</returns>
    public static SceneId New() => new(Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Gets an invalid scene identifier.
    /// </summary>
    public static readonly SceneId Invalid = new(string.Empty);
}
