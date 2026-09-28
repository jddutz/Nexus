namespace Nexus.Game;

/// <summary>
/// Provides the default scene loader.
/// </summary>
public class SceneRegistry : ISceneRegistry
{
    private readonly Dictionary<SceneId, Func<IScene>> _sceneFactories = [];

    /// <inheritdoc/>
    public void Register(SceneId sceneId, Func<IScene> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_sceneFactories.TryAdd(sceneId, factory))
            throw new ArgumentException(
                $"A factory is already registered for scene '{sceneId}'.",
                nameof(sceneId)
            );
    }

    /// <inheritdoc/>
    public IScene? Load(SceneId sceneId)
    {
        if (!_sceneFactories.TryGetValue(sceneId, out var factory))
            return null;

        return factory()
            ?? throw new InvalidOperationException(
                $"The factory for scene '{sceneId}' returned null."
            );
    }
}
