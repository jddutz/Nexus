namespace Nexus.Game;

/// <summary>
/// Provides the default scene loader.
/// </summary>
public class SceneRegistry : ISceneRegistry
{
    private readonly Dictionary<string, Func<IScene>> _sceneFactories = [];

    /// <inheritdoc/>
    public void Register(string sceneName, Func<IScene> factory)
    {
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
        if (!_sceneFactories.TryGetValue(sceneName, out var factory))
            return null;

        return factory()
            ?? throw new InvalidOperationException(
                $"The factory for scene '{sceneName}' returned null."
            );
    }
}
