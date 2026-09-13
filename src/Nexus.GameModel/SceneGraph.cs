namespace Nexus.Core.Scenes;

/// <summary>
/// Provides the default scene graph implementation.
/// </summary>
public class SceneGraph : ISceneGraph
{
    /// <summary>
    /// Gets the default initial scene identifier.
    /// </summary>
    public SceneId InitialSceneId => default;

    /// <inheritdoc />
    public void Update(double deltaTime) { }
}
