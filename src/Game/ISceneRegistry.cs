namespace Nexus.Game;

/// <summary>
/// Loads scenes by identifier.
/// </summary>
public interface ISceneRegistry
{
    /// <summary>
    /// Loads the scene with the specified identifier.
    /// </summary>
    /// <param name="sceneId">The identifier of the scene to load.</param>
    /// <returns>The loaded scene, or <see langword="null"/> when it cannot be loaded.</returns>
    IScene? Load(SceneId sceneId);
}