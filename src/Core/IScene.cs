namespace Nexus.Core;

/// <summary>
/// Defines a scene that is the root of a scene-node hierarchy.
/// </summary>
public interface IScene : ISceneNode, IManagedEntity, IObservable
{
    /// <summary>
    /// Gets every node in the scene, including the scene node itself.
    /// </summary>
    IReadOnlyDictionary<NodeId, ISceneNode> AllSceneNodes { get; }

    /// <summary>
    /// Gets a value indicating whether this scene is active.
    /// </summary>
    bool IsActive { get; }
}
