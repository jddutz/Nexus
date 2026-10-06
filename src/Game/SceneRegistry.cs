namespace Nexus.Game;

/// <summary>Discovers concrete scene classes and loads scenes by name.</summary>
public class SceneRegistry : ISceneRegistry
{
    private readonly Dictionary<string, Func<IScene>> _sceneFactories = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <inheritdoc />
    public int SceneCount => _sceneFactories.Count;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RegisteredScenes => _sceneFactories.Keys;

    /// <summary>Creates the registry and discovers scenes in the configured assemblies.</summary>
    /// <param name="services">The provider used to construct scenes when loaded.</param>
    /// <param name="options">The scene discovery settings.</param>
    public SceneRegistry(IServiceProvider services, IOptions<SceneRegistrySettings> options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        var assemblies = new HashSet<Assembly>(settings.Assemblies);
        if (settings.ScanEntryAssembly && Assembly.GetEntryAssembly() is { } entryAssembly)
            assemblies.Add(entryAssembly);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (
                    !type.IsClass
                    || type.IsAbstract
                    || type.ContainsGenericParameters
                    || !typeof(IScene).IsAssignableFrom(type)
                )
                    continue;

                var attribute = type.GetCustomAttribute<SceneAttribute>(inherit: false);
                var sceneName = attribute?.Name ?? type.Name;
                if (string.IsNullOrWhiteSpace(sceneName))
                    throw new InvalidOperationException(
                        $"Scene type '{type.FullName}' has an empty registration name."
                    );

                Register(
                    sceneName,
                    () => (IScene)ActivatorUtilities.CreateInstance(services, type)
                );
            }
        }
    }

    /// <inheritdoc/>
    public void Register(string sceneName, Func<IScene> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
        ArgumentNullException.ThrowIfNull(factory);
        if (!_sceneFactories.TryAdd(sceneName, factory))
            throw new ArgumentException(
                $"A factory is already registered for scene '{sceneName}'.",
                nameof(sceneName)
            );

        PropertyChanged?.Invoke(nameof(RegisteredScenes));
    }

    /// <inheritdoc/>
    public IScene? Load(string sceneName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
        if (!_sceneFactories.TryGetValue(sceneName, out var factory))
            return null;

        return factory()
            ?? throw new InvalidOperationException(
                $"The factory for scene '{sceneName}' returned null."
            );
    }
}
