namespace Nexus.Game;

/// <summary>
/// Configures scene discovery.
/// </summary>
public sealed class SceneRegistrySettings
{
    /// <summary>Gets or sets whether the entry assembly is scanned for scenes.</summary>
    public bool ScanEntryAssembly { get; set; } = true;

    /// <summary>Gets additional assemblies to scan for concrete scene implementations.</summary>
    public ISet<Assembly> Assemblies { get; } = new HashSet<Assembly>();

    /// <summary>Adds an assembly to the scene discovery set.</summary>
    /// <param name="assembly">The assembly to scan.</param>
    public void AddAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Assemblies.Add(assembly);
    }

    /// <summary>Adds the assembly containing the specified type to the scene discovery set.</summary>
    public void AddAssemblyContaining<T>() => AddAssembly(typeof(T).Assembly);
}
