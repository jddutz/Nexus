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
        _sceneFactories[sceneId] = factory;
    }

    /// <inheritdoc/>
    public IScene? Load(SceneId sceneId) =>
        _sceneFactories.TryGetValue(sceneId, out var factory) ? factory() : null;
}
