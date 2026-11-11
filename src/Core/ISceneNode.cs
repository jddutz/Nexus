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
    /// Looks up descendants of this node using an index.
    /// </summary>
    /// <typeparam name="TNode">The type of nodes to look up.</typeparam>
    /// <param name="predicate">Predicate used to filter results.</param>
    /// <returns>The matching descendants, or an empty sequence when no nodes match.</returns>
    IEnumerable<TNode> Find<TNode>(Predicate<TNode>? predicate = null)
        where TNode : ISceneNode;

    /// <summary>
    /// Called when this node's parent or ancestor chain changes,
    /// including when its subtree is detached from a scene.
    /// Implementations update their hierarchy-dependent state,
    /// then notify each child.
    /// </summary>
    void OnSceneHierarchyChanged();

    /// <summary>
    /// Gets or sets the scene object factory used to create new nodes.
    /// </summary>
    ISceneObjectFactory Factory { get; set; }

    /// <summary>
    /// Creates a new instance of the specified scene node using Factory.
    /// </summary>
    /// <typeparam name="T">The type of node to be created.</typeparam>
    /// <param name="args">Required constructor parameters.</param>
    /// <returns>An instance of type T.</returns>
    T Create<T>(params object[] args)
        where T : ISceneNode;
}
