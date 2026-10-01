namespace Nexus.Game;

/// <summary>
/// Provides the root node and node lookup for a scene hierarchy.
/// </summary>
public partial class Scene : IScene
{
    private readonly SceneNodeId _sceneNodeId = SceneNodeId.New();
    private readonly ObservableCollection<ISceneNode> _children = [];
    private readonly Dictionary<SceneNodeId, ISceneNode> _allSceneNodes = [];

    [Observable(PublicSetter = true)]
    private InputMap? _inputMap;

    [Observable(PublicSetter = false)]
    private bool _isInitialized;

    [Observable(PublicSetter = false)]
    private bool _isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier.
    /// </summary>
    /// <param name="sceneId">The identifier for the scene.</param>
    public Scene(SceneId sceneId)
    {
        Id = sceneId;
        _children.ItemAdded += OnRootChildAdded;
        _children.ItemRemoved += OnRootChildRemoved;
        _children.ItemAdded += OnChildAdded;
        _children.ItemRemoved += OnChildRemoved;
        _allSceneNodes.Add(_sceneNodeId, this);

        Layers = new RenderLayerCollection();
        Layers.Create("GUI", RenderPasses.Main);

        var defaultCamera = new StaticCamera();
        var viewComponent = new ViewComponent() { Camera = defaultCamera, LayerMask = 1 };
        var defaultView = new GameObject2D([defaultCamera, viewComponent]);
        Children.Add(defaultView);
    }

    /// <summary>
    /// Gets the unique identifier for this scene.
    /// </summary>
    public SceneId Id { get; }

    /// <inheritdoc />
    SceneNodeId ISceneNode.Id => _sceneNodeId;

    /// <inheritdoc />
    IScene? ISceneNode.Scene
    {
        get => this;
        set
        {
            if (!ReferenceEquals(value, this))
                throw new InvalidOperationException("A scene cannot belong to another scene.");
        }
    }

    /// <inheritdoc />
    ISceneNode? ISceneNode.Parent
    {
        get => null;
        set
        {
            if (value is not null)
                throw new InvalidOperationException("A scene cannot have a parent.");
        }
    }

    /// <inheritdoc />
    public IObservableCollection<ISceneNode> Children => _children;

    /// <inheritdoc />
    public IReadOnlyDictionary<SceneNodeId, ISceneNode> AllSceneNodes => _allSceneNodes;

    /// <summary>
    /// Gets the current set of render layers managed by the graphics system.
    /// </summary>
    internal RenderLayerCollection Layers { get; }

    /// <inheritdoc />
    public bool IsActivated => _isActive;

    /// <summary>
    /// Gets a scene node by its identifier, or <see langword="null"/> when the identifier is absent.
    /// </summary>
    /// <param name="sceneNodeId">The identifier of the node to find.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    public ISceneNode? GetSceneNode(SceneNodeId sceneNodeId) =>
        _allSceneNodes.GetValueOrDefault(sceneNodeId);

    /// <inheritdoc />
    public void Initialize()
    {
        if (_isInitialized)
            return;

        SetIsInitialized(true);
    }

    /// <inheritdoc />
    public bool CanActivate() => true;

    /// <inheritdoc />
    public void Activate()
    {
        if (_isActive || !CanActivate())
            return;

        SetIsActive(true);

        PropertyChanged?.Invoke(nameof(IsActivated));
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    /// <inheritdoc />
    public void Deactivate()
    {
        if (!_isActive)
            return;

        SetIsActive(false);
        PropertyChanged?.Invoke(nameof(IsActivated));
    }

    /// <summary>
    /// Creates and adds a root game object of the specified type.
    /// </summary>
    /// <typeparam name="TChild">The type of game object to create.</typeparam>
    /// <returns>The added game object.</returns>
    public TChild CreateChild<TChild>()
        where TChild : IGameObject, new()
    {
        var child = new TChild();
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Registers an added child and all of its existing descendants in this scene.
    /// </summary>
    /// <param name="child">The added child.</param>
    private void OnChildAdded(ISceneNode child)
    {
        if (child.Scene is not null && !ReferenceEquals(child.Scene, this))
            throw new InvalidOperationException("Scene node already belongs to another scene.");

        if (
            _allSceneNodes.TryGetValue(child.Id, out var registeredNode)
            && ReferenceEquals(registeredNode, child)
        )
            return;

        var addedNodes = EnumerateSubtree(child).ToArray();
        try
        {
            ValidateSubtree(addedNodes);
        }
        catch
        {
            child.Parent?.Children.Remove(child);
            throw;
        }

        RegisterSubtree(child);
    }

    /// <summary>
    /// Unregisters a removed child and all of its descendants from this scene.
    /// </summary>
    /// <param name="child">The removed child.</param>
    private void OnChildRemoved(ISceneNode child)
    {
        if (
            !_allSceneNodes.TryGetValue(child.Id, out var registeredNode)
            || !ReferenceEquals(registeredNode, child)
        )
            return;

        var removedNodes = EnumerateSubtree(child).ToArray();
        foreach (var node in removedNodes.Reverse())
        {
            node.Children.ItemAdded -= OnChildAdded;
            node.Children.ItemRemoved -= OnChildRemoved;
            if (
                _allSceneNodes.TryGetValue(node.Id, out var trackedNode)
                && ReferenceEquals(trackedNode, node)
            )
                _allSceneNodes.Remove(node.Id);
            if (ReferenceEquals(node.Scene, this))
                node.Scene = null;
        }
    }

    /// <summary>
    /// Sets the scene as the parent of a newly added root node.
    /// </summary>
    /// <param name="child">The root node added to this scene.</param>
    private void OnRootChildAdded(ISceneNode child)
    {
        if (child.Parent is null)
            child.Parent = this;
        else if (!ReferenceEquals(child.Parent, this))
            throw new InvalidOperationException("Scene node already belongs to another parent.");
    }

    /// <summary>
    /// Clears the scene parent of a removed root node.
    /// </summary>
    /// <param name="child">The root node removed from this scene.</param>
    private void OnRootChildRemoved(ISceneNode child)
    {
        if (ReferenceEquals(child.Parent, this))
            child.Parent = null;
    }

    /// <summary>
    /// Adds a node and its existing descendants to the scene lookup and subscriptions.
    /// </summary>
    /// <param name="node">The root node of the subtree to register.</param>
    private void RegisterSubtree(ISceneNode node)
    {
        if (_allSceneNodes.TryGetValue(node.Id, out var existingNode))
        {
            if (ReferenceEquals(existingNode, node))
                return;

            throw new InvalidOperationException($"Scene node id '{node.Id}' is already in use.");
        }

        _allSceneNodes.Add(node.Id, node);
        node.Scene = this;
        node.Children.ItemAdded += OnChildAdded;
        node.Children.ItemRemoved += OnChildRemoved;

        foreach (var child in node.Children.ToArray())
        {
            if (child.Parent is null)
                child.Parent = node;
            RegisterSubtree(child);
        }
    }

    /// <summary>
    /// Verifies that a subtree has unique identifiers and contains no node owned by another scene.
    /// </summary>
    /// <param name="nodes">The nodes to validate.</param>
    private void ValidateSubtree(IEnumerable<ISceneNode> nodes)
    {
        var identifiers = new HashSet<SceneNodeId>();
        foreach (var node in nodes)
        {
            if (!identifiers.Add(node.Id))
                throw new InvalidOperationException($"Scene node id '{node.Id}' is duplicated.");
            if (node.Scene is not null && !ReferenceEquals(node.Scene, this))
                throw new InvalidOperationException("Scene node already belongs to another scene.");
            if (
                _allSceneNodes.TryGetValue(node.Id, out var registeredNode)
                && !ReferenceEquals(registeredNode, node)
            )
                throw new InvalidOperationException(
                    $"Scene node id '{node.Id}' is already in use."
                );
        }
    }

    /// <summary>
    /// Enumerates a node and all of its descendants in parent-first order.
    /// </summary>
    /// <param name="node">The root node to enumerate.</param>
    /// <returns>The node and its descendants.</returns>
    private static IEnumerable<ISceneNode> EnumerateSubtree(ISceneNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in EnumerateSubtree(child))
            yield return descendant;
    }
}
