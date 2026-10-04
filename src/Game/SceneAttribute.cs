namespace Nexus.Game;

/// <summary>
/// Overrides the default registration name of an automatically discovered scene.
/// </summary>
/// <param name="name">The non-whitespace registration name.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SceneAttribute(string name) : Attribute
{
    /// <summary>Gets the registration name for the scene.</summary>
    public string Name { get; } = ValidateName(name);

    /// <summary>Validates the required registration name.</summary>
    /// <param name="name">The registration name to validate.</param>
    /// <returns>The validated registration name.</returns>
    /// <exception cref="ArgumentException">The name is null, empty, or whitespace.</exception>
    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name;
    }
}
