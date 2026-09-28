namespace Nexus.Game;

/// <summary>
/// Loads scenes by identifier.
/// </summary>
public interface ISceneRegistry
{
    /// <summary>
    /// Registers a factory for the specified scene identifier.
    /// </summary>
    /// <param name="sceneId">The identifier associated with the factory.</param>
    /// <param name="factory">Creates the scene when it is loaded.</param>
    /// <exception cref="ArgumentException">A factory is already registered for the identifier.</exception>
    void Register(SceneId sceneId, Func<IScene> factory);

    /// <summary>
    /// Loads the scene with the specified identifier.
    /// </summary>
    /// <param name="sceneId">The identifier of the scene to load.</param>
    /// <returns>The loaded scene, or <see langword="null"/> when it cannot be loaded.</returns>
    /// <exception cref="InvalidOperationException">The registered factory returns <see langword="null"/>.</exception>
    IScene? Load(SceneId sceneId);
}
