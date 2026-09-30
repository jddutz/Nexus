namespace Nexus.Core;

/// <summary>
/// Defines a scene that is the root of a scene-node hierarchy.
/// </summary>
public interface IScene : ISceneNode, IManagedEntity, IObservable
{
    /// <summary>
    /// Gets every node in the scene, including the scene node itself.
    /// </summary>
    IReadOnlyDictionary<SceneNodeId, ISceneNode> AllSceneNodes { get; }

    /// <summary>
    /// Gets a value indicating whether this scene is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Occurs when a component is added to a game object in this scene.
    /// </summary>
    event Action<IComponent>? ComponentAdded;

    /// <summary>
    /// Occurs when a component is removed from a game object in this scene.
    /// </summary>
    event Action<IComponent>? ComponentRemoved;

    /// <summary>
    /// Occurs when a game object is added to this scene or one of its descendants.
    /// </summary>
    event Action<IGameObject>? GameObjectAdded;

    /// <summary>
    /// Occurs when a game object is removed from this scene or one of its descendants.
    /// </summary>
    event Action<IGameObject>? GameObjectRemoved;
}
