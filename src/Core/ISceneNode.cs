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
    /// Gets the containing scene, or null when detached.
    /// </summary>
    IScene? Scene { get; set; }

    /// <summary>
    /// Gets the parent node, or <see langword="null"/> when this node is a root or is detached.
    /// </summary>
    ISceneNode? Parent { get; set; }

    /// <summary>
    /// Gets the scene nodes directly contained by this node.
    /// </summary>
    IObservableCollection<ISceneNode> Children { get; }
}
