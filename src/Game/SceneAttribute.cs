namespace Nexus.Game;

/// <summary>
/// Overrides the default registration name of an automatically discovered scene.
/// </summary>
/// <param name="name">
/// The registration name. When omitted, the scene class name is used.
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SceneAttribute(string? name = null) : Attribute
{
    /// <summary>Gets the optional scene registration name.</summary>
    public string? Name { get; } = name;
}
