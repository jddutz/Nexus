namespace Nexus.Core;

/// <summary>
/// Defines a scene that is the root of a scene-node hierarchy.
/// </summary>
public interface IScene : ISceneNode, IManagedEntity, IObservable
{
    /// <summary>
    /// Gets whether this scene is loaded and active in a game system.
    /// </summary>
    bool IsLoaded => IsActivated;
}
