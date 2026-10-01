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
    ISceneNode? Root { get; set; }

    /// <summary>
    /// Gets the parent node, or <see langword="null"/> when this node is detached.
    /// </summary>
    ISceneNode? Parent { get; set; }

    /// <summary>
    /// Gets the scene nodes directly contained by this node.
    /// </summary>
    IObservableCollection<ISceneNode> Children { get; }

    /// <summary>
    /// Called when this node's parent or ancestor chain changes,
    /// including when its subtree is detached from a scene.
    /// Implementations update their hierarchy-dependent state,
    /// then notify each child.
    /// </summary>
    void OnSceneHierarchyChanged();
}
