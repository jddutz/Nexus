namespace Nexus.Game;

/// <summary>
/// Discovers attributed scene classes and loads scenes by name.
/// </summary>
public class SceneRegistry : ISceneRegistry
{
    private readonly Dictionary<string, Func<IScene>> _sceneFactories = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RegisteredSceneNames => _sceneFactories.Keys;

    /// <summary>Creates an empty registry for manually registered scenes.</summary>
    public SceneRegistry()
        : this(new EmptyServiceProvider(), Options.Create(new SceneRegistrySettings()))
    {
    }

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
                var attribute = type.GetCustomAttribute<SceneAttribute>(inherit: false);
                if (attribute is null)
                    continue;

                if (
                    !type.IsClass
                    || type.IsAbstract
                    || type.ContainsGenericParameters
                    || !typeof(IScene).IsAssignableFrom(type)
                )
                    throw new InvalidOperationException(
                        $"Scene type '{type.FullName}' must be a concrete, non-generic class implementing IScene."
                    );

                var sceneName = attribute.Name ?? type.Name;
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

    /// <summary>Provides no services for parameterless registry instances.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>Returns no service.</summary>
        /// <param name="serviceType">The requested service type.</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public object? GetService(Type serviceType) => null;
    }
}
