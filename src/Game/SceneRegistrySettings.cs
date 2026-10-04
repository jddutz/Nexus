namespace Nexus.Game;

public sealed class SceneRegistrySettings
{
    public bool ScanEntryAssembly { get; set; } = true;

    public ISet<Assembly> Assemblies { get; } = new HashSet<Assembly>();

    public void AddAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Assemblies.Add(assembly);
    }

    public void AddAssemblyContaining<T>() => AddAssembly(typeof(T).Assembly);
}
