namespace Nexus.Game;

/// <summary>
/// Provides the root node and node lookup for a scene hierarchy.
/// </summary>
public partial class Scene : IScene
{
    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <summary>Initializes a scene with a generated identifier.</summary>
    public Scene()
        : this(NodeId.New()) { }

    private readonly ObservableCollection<ISceneNode> _children = new(
        ReferenceEqualityComparer.Instance
    );
    private readonly Dictionary<NodeId, ISceneNode> _allNodes = [];

    [Observable(PublicSetter = true)]
    private RenderLayerCollection _renderLayers = new();

    [Observable(PublicSetter = true)]
    private InputMap? _inputMap;

    [Observable(PublicSetter = true, Required = true)]
    private ICamera _mainCamera = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The node identifier for the scene.</param>
    public Scene(NodeId id)
    {
        Id = id;

        _children.ValidationRules += ValidateChild;
        _children.ItemAdded += OnChildAdded;
        _children.ItemRemoved += OnChildRemoved;

        _allNodes.Add(Id, this);

        RenderLayers.Create("GUI", RenderPasses.Main);
    }

    /// <summary>
    /// Gets the unique identifier for this scene.
    /// </summary>
    public NodeId Id { get; }

    /// <summary>Gets whether the scene is currently active.</summary>
    public bool IsActive => IsActivated;

    /// <summary>Gets a registered scene node by identifier.</summary>
    /// <param name="id">The node identifier.</param>
    /// <returns>The registered node, or <see langword="null"/>.</returns>
    public ISceneNode? GetSceneNode(NodeId id) => _allNodes.GetValueOrDefault(id);

    /// <summary>Creates and adds a child node of the requested type.</summary>
    /// <typeparam name="TChild">The child node type.</typeparam>
    /// <returns>The newly created child.</returns>
    public TChild CreateChild<TChild>()
        where TChild : ISceneNode, new()
    {
        var child = new TChild();
        Children.Add(child);
        return child;
    }

    /// <inheritdoc />
    public ISceneNode? Root => this;

    /// <inheritdoc />
    ISceneNode? ISceneNode.Root
    {
        get => this;
        set
        {
            if (!ReferenceEquals(value, this))
                throw new InvalidOperationException("A scene is always its own root.");
        }
    }

    /// <summary>Gets or sets the parent node, which is always null for a scene.</summary>
    /// <exception cref="InvalidOperationException">The assigned value is not null.</exception>
    public virtual ISceneNode? Parent
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

    /// <summary>
    /// Determines whether a node can be added without creating duplicate ownership or a cycle.
    /// </summary>
    /// <param name="node">The node to validate.</param>
    /// <returns><see langword="true"/> when the node can be added; otherwise, <see langword="false"/>.</returns>
    private bool ValidateChild(ISceneNode node)
    {
        if (node is null || node is IScene || node.Parent is not null)
            return false;

        for (ISceneNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, node))
                return false;
        }

        return !_children.Contains(node) && ValidateSceneNodeIds(node);
    }

    /// <summary>
    /// Validates IDs in an incoming node graph against each other and this scene.
    /// </summary>
    /// <param name="node">The incoming node.</param>
    /// <returns><see langword="true"/> when all IDs are unique; otherwise, <see langword="false"/>.</returns>
    private bool ValidateSceneNodeIds(ISceneNode node)
    {
        var nodeIds = new HashSet<NodeId>();
        var visitedNodes = new HashSet<ISceneNode>(ReferenceEqualityComparer.Instance);
        var nodesToVisit = new Stack<ISceneNode>();
        nodesToVisit.Push(node);

        while (nodesToVisit.TryPop(out var current))
        {
            if (
                !visitedNodes.Add(current)
                || !nodeIds.Add(current.Id)
                || _allNodes.ContainsKey(current.Id)
            )
                return false;

            foreach (var child in current.Children)
                nodesToVisit.Push(child);
        }

        return true;
    }

    [Observable(PublicSetter = false)]
    private bool _isInitialized = false;

    /// <inheritdoc />
    public virtual void Initialize()
    {
        if (IsInitialized)
            return;

        IsInitialized = true;
    }

    [Observable(PublicSetter = false)]
    private bool _isActivated = false;

    /// <inheritdoc />
    public virtual bool CanActivate() => true;

    /// <inheritdoc />
    public virtual void Activate()
    {
        if (IsActivated || !CanActivate())
            return;

        IsActivated = true;
    }

    /// <inheritdoc />
    public virtual void Update(double deltaTime) { }

    /// <inheritdoc />
    public virtual void Deactivate()
    {
        if (!IsActivated)
            return;

        IsActivated = false;
    }

    private void OnChildAdded(ISceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.Parent = this;
        Register(node);
    }

    private void OnChildRemoved(ISceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.Parent = null;
        Unregister(node);
    }

    /// <inheritdoc />
    public virtual void OnSceneHierarchyChanged()
    {
        foreach (var child in _children)
            child.OnSceneHierarchyChanged();
    }

    /// <summary>
    /// Adds a node and its existing descendants to the scene lookup and subscriptions.
    /// </summary>
    /// <param name="node">The root node of the subtree to register.</param>
    private void Register(ISceneNode node)
    {
        if (_allNodes.TryGetValue(node.Id, out var registeredNode))
        {
            if (ReferenceEquals(registeredNode, node))
                return;

            throw new InvalidOperationException(
                $"Duplicate Node Id: {node.Id}. A different node is already registered with the same Node Id."
            );
        }

        node.Root = this;

        node.Children.ValidationRules += ValidateSceneNodeIds;
        node.Children.ItemAdded += Register;
        node.Children.ItemRemoved += Unregister;

        _allNodes.Add(node.Id, node);

        foreach (var child in node.Children)
            Register(child);

        node.OnSceneHierarchyChanged();
    }

    /// <summary>
    /// Removes a node and its descendants from the scene lookup and subscriptions.
    /// </summary>
    /// <param name="node">The root node of the subtree to unregister.</param>
    private void Unregister(ISceneNode node)
    {
        if (!_allNodes.TryGetValue(node.Id, out var registeredNode))
            return;

        if (!ReferenceEquals(registeredNode, node))
            return;

        foreach (var child in node.Children)
            Unregister(child);

        node.Root = null;

        node.Children.ValidationRules -= ValidateSceneNodeIds;
        node.Children.ItemAdded -= Register;
        node.Children.ItemRemoved -= Unregister;

        _allNodes.Remove(node.Id);
        node.OnSceneHierarchyChanged();
    }
}
