namespace Nexus.Game;

/// <summary>
/// Provides the default scene loader.
/// </summary>
public class SceneRegistry : ISceneRegistry
{
    /// <inheritdoc/>
    public IScene? Load(SceneId sceneId) => new Scene(sceneId);
}