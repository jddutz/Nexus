namespace Nexus.Core;

/// <summary>
/// Defines a node in a scene's game object hierarchy.
/// </summary>
public interface ISceneNode
{
    /// <summary>
    /// Gets the unique identifier of this scene node.
    /// </summary>
    NodeId Id { get; }

    /// <summary>
    /// Gets the root node, or null when detached.
    /// </summary>
    ISceneNode? Root { get; }

    /// <summary>
    /// Sets the root node that owns this node, or null when detached.
    /// </summary>
    /// <param name="root">The owning root node, or null when detached.</param>
    void SetRoot(ISceneNode? root);

    /// <summary>
    /// Gets the parent node, or <see langword="null"/> when this node is detached.
    /// </summary>
    ISceneNode? Parent { get; }

    /// <summary>
    /// Sets the parent node, or <see langword="null"/> when this node is detached.
    /// </summary>
    void SetParent(ISceneNode? parent);

    /// <summary>
    /// Gets the scene nodes directly contained by this node.
    /// </summary>
    IObservableCollection<ISceneNode> Children { get; }
}
