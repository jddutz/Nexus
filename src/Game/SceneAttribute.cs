namespace Nexus.Game;

/// <summary>
/// Marks a scene class for automatic registration.
/// </summary>
/// <param name="name">
/// The registration name. When omitted, the class name is used.
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SceneAttribute(string? name = null) : Attribute
{
    /// <summary>Gets the optional scene registration name.</summary>
    public string? Name { get; } = name;
}
