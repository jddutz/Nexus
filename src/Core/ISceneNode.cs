namespace Nexus.Core;

/// <summary>
/// Defines a node in a scene's game object hierarchy.
/// </summary>
public interface ISceneNode
{
    /// <summary>
    /// Gets the unique identifier of this scene node.
    /// </summary>
    GameObjectId Id { get; }

    /// <summary>
    /// Gets the containing scene, or null when detached.
    /// </summary>
    IScene? Scene { get; set; }

    /// <summary>
    /// Gets the parent node, or <see langword="null"/> when this node is a root or is detached.
    /// </summary>
    ISceneNode? Parent { get; set; }

    /// <summary>
    /// Gets the game objects directly contained by this node.
    /// </summary>
    IReadOnlyObservableCollection<IGameObject> Children { get; }

    /// <summary>
    /// Occurs when a child is added to the scene node.
    /// </summary>
    event Action<ISceneNode>? ChildAdded;
}
