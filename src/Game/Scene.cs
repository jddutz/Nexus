namespace Nexus.Game;

/// <summary>
/// Provides the root node and node lookup for a scene hierarchy.
/// </summary>
public class Scene : IScene
{
    private readonly SceneNodeId _sceneNodeId = SceneNodeId.New();
    private readonly ObservableCollection<ISceneNode> _children = [];
    private readonly Dictionary<SceneNodeId, ISceneNode> _allSceneNodes = [];
    private readonly Dictionary<
        ISceneNode,
        (Action<ISceneNode> Added, Action<ISceneNode> Removed)
    > _childCollectionHandlers = new(ReferenceEqualityComparer.Instance);
    private IInputSystem? _inputSystem;
    private InputMap? _inputMap;
    private bool _isInitialized;
    private bool _isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified input system.
    /// </summary>
    /// <param name="inputSystem">The input system used to select this scene's input map.</param>
    public Scene(IInputSystem inputSystem)
        : this(SceneId.New(), inputSystem) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier.
    /// </summary>
    /// <param name="sceneId">The identifier for the scene.</param>
    public Scene(SceneId sceneId)
        : this(sceneId, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier and input system.
    /// </summary>
    /// <param name="sceneId">The identifier for the scene.</param>
    /// <param name="inputSystem">The input system used to select this scene's input map.</param>
    public Scene(SceneId sceneId, IInputSystem? inputSystem)
    {
        Id = sceneId;
        _inputSystem = inputSystem;
        _allSceneNodes.Add(_sceneNodeId, this);
        SubscribeToChildren(this);

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
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public bool IsActivated => _isActive;

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <inheritdoc />
    public event Action<IComponent>? ComponentAdded;

    /// <inheritdoc />
    public event Action<IComponent>? ComponentRemoved;

    /// <inheritdoc />
    public event Action<IGameObject>? GameObjectAdded;

    /// <inheritdoc />
    public event Action<IGameObject>? GameObjectRemoved;

    /// <summary>
    /// Gets or sets the input map selected while this scene is active.
    /// </summary>
    public InputMap? InputMap
    {
        get => _inputMap;
        set
        {
            if (ReferenceEquals(_inputMap, value))
                return;

            var previousMap = _inputMap;
            _inputMap = value;

            if (
                _isActive
                && _inputSystem is not null
                && ReferenceEquals(_inputSystem.CurrentMap, previousMap)
            )
                _inputSystem.CurrentMap = value;

            PropertyChanged?.Invoke(nameof(InputMap));
        }
    }

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

        _isInitialized = true;
        PropertyChanged?.Invoke(nameof(IsInitialized));
    }

    /// <inheritdoc />
    public bool CanActivate() => true;

    /// <inheritdoc />
    public void Activate()
    {
        if (_isActive || !CanActivate())
            return;

        _isActive = true;
        if (_inputSystem is not null)
            _inputSystem.CurrentMap = _inputMap;

        PropertyChanged?.Invoke(nameof(IsActive));
        PropertyChanged?.Invoke(nameof(IsActivated));
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    /// <inheritdoc />
    public void Deactivate()
    {
        if (!_isActive)
            return;

        if (_inputSystem is not null && ReferenceEquals(_inputSystem.CurrentMap, _inputMap))
            _inputSystem.CurrentMap = null;

        _isActive = false;
        PropertyChanged?.Invoke(nameof(IsActive));
        PropertyChanged?.Invoke(nameof(IsActivated));
    }

    /// <summary>
    /// Adds a root scene node to this scene.
    /// </summary>
    /// <param name="child">The node to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The node already belongs to a parent or scene.</exception>
    public void AddChild(ISceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null || child.Scene is not null)
            throw new InvalidOperationException("Scene node already belongs to a parent or scene.");

        Children.Add(child);
    }

    /// <summary>
    /// Removes a root scene node from this scene.
    /// </summary>
    /// <param name="child">The node to remove.</param>
    /// <returns><see langword="true"/> when the node was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    public bool RemoveChild(ISceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return Children.Remove(child);
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
        AddChild(child);
        return child;
    }

    /// <summary>
    /// Associates this scene with the input system that selects its map during activation.
    /// </summary>
    /// <param name="inputSystem">The input system to associate, or <see langword="null"/>.</param>
    internal void SetInputSystem(IInputSystem? inputSystem)
    {
        if (ReferenceEquals(_inputSystem, inputSystem))
            return;

        var wasSelected =
            _isActive
            && _inputSystem is not null
            && ReferenceEquals(_inputSystem.CurrentMap, _inputMap);
        if (wasSelected)
            _inputSystem!.CurrentMap = null;

        _inputSystem = inputSystem;
        if (_isActive && _inputSystem is not null)
            _inputSystem.CurrentMap = _inputMap;
    }

    /// <summary>
    /// Subscribes to the child and component collections of a registered node.
    /// </summary>
    /// <param name="node">The scene node to observe.</param>
    private void SubscribeToChildren(ISceneNode node)
    {
        Action<ISceneNode> addedHandler = child => OnChildAdded(node, child);
        Action<ISceneNode> removedHandler = child => OnChildRemoved(node, child);
        _childCollectionHandlers.Add(node, (addedHandler, removedHandler));
        node.Children.ItemAdded += addedHandler;
        node.Children.ItemRemoved += removedHandler;

        if (node is IGameObject gameObject)
        {
            gameObject.Components.ItemAdded += OnComponentAdded;
            gameObject.Components.ItemRemoved += OnComponentRemoved;
        }
    }

    /// <summary>
    /// Unsubscribes from the child and component collections of a removed node.
    /// </summary>
    /// <param name="node">The scene node to stop observing.</param>
    private void UnsubscribeFromChildren(ISceneNode node)
    {
        if (_childCollectionHandlers.Remove(node, out var handlers))
        {
            node.Children.ItemAdded -= handlers.Added;
            node.Children.ItemRemoved -= handlers.Removed;
        }

        if (node is IGameObject gameObject)
        {
            gameObject.Components.ItemAdded -= OnComponentAdded;
            gameObject.Components.ItemRemoved -= OnComponentRemoved;
        }
    }

    /// <summary>
    /// Registers an added child and all of its existing descendants in this scene.
    /// </summary>
    /// <param name="parent">The parent whose child collection changed.</param>
    /// <param name="child">The added child.</param>
    private void OnChildAdded(ISceneNode parent, ISceneNode child)
    {
        if (child.Parent is null)
            child.Parent = parent;
        else if (!ReferenceEquals(child.Parent, parent))
            throw new InvalidOperationException("Scene node already belongs to another parent.");

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
            parent.Children.Remove(child);
            throw;
        }

        RegisterSubtree(child);
        if (_isActive)
        {
            foreach (var gameObject in addedNodes.OfType<IGameObject>())
                GameObjectAdded?.Invoke(gameObject);

            foreach (
                var component in addedNodes
                    .OfType<IGameObject>()
                    .SelectMany(node => node.Components)
            )
                ComponentAdded?.Invoke(component);
        }
    }

    /// <summary>
    /// Unregisters a removed child and all of its descendants from this scene.
    /// </summary>
    /// <param name="parent">The parent whose child collection changed.</param>
    /// <param name="child">The removed child.</param>
    private void OnChildRemoved(ISceneNode parent, ISceneNode child)
    {
        if (
            !_allSceneNodes.TryGetValue(child.Id, out var registeredNode)
            || !ReferenceEquals(registeredNode, child)
        )
        {
            if (ReferenceEquals(child.Parent, parent))
                child.Parent = null;
            return;
        }

        var removedNodes = EnumerateSubtree(child).ToArray();
        if (_isActive)
        {
            foreach (var node in removedNodes.Reverse())
            {
                if (node is IGameObject gameObject)
                    GameObjectRemoved?.Invoke(gameObject);

                if (node is IGameObject componentOwner)
                    foreach (var component in componentOwner.Components.Reverse())
                        ComponentRemoved?.Invoke(component);
            }
        }

        foreach (var node in removedNodes.Reverse())
        {
            UnsubscribeFromChildren(node);
            if (
                _allSceneNodes.TryGetValue(node.Id, out var trackedNode)
                && ReferenceEquals(trackedNode, node)
            )
                _allSceneNodes.Remove(node.Id);
            if (ReferenceEquals(node.Scene, this))
                node.Scene = null;
        }

        if (ReferenceEquals(child.Parent, parent))
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
        SubscribeToChildren(node);

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

    /// <summary>
    /// Forwards component additions while this scene is active.
    /// </summary>
    /// <param name="component">The added component.</param>
    private void OnComponentAdded(IComponent component)
    {
        if (_isActive)
            ComponentAdded?.Invoke(component);
    }

    /// <summary>
    /// Forwards component removals while this scene is active.
    /// </summary>
    /// <param name="component">The removed component.</param>
    private void OnComponentRemoved(IComponent component)
    {
        if (_isActive)
            ComponentRemoved?.Invoke(component);
    }
}
