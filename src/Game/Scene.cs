namespace Nexus.Game;

/// <summary>
/// Provides the root node and node lookup for a scene hierarchy.
/// </summary>
public partial class Scene : IScene
{
    private readonly ObservableCollection<ISceneNode> _children = [];
    private readonly Dictionary<NodeId, ISceneNode> _allNodes = [];

    [Observable(PublicSetter = true)]
    private RenderLayerCollection _renderLayers = new();

    [Observable(PublicSetter = true)]
    private InputMap? _inputMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The node identifier for the scene.</param>
    public Scene(NodeId id)
    {
        Id = id;
        _children.ItemAdded += Register;
        _children.ItemRemoved += Unregister;
        _allNodes.Add(Id, this);

        RenderLayers.Create("GUI", RenderPasses.Main);

        var defaultCamera = new StaticCamera();
        var viewComponent = new ViewComponent() { Camera = defaultCamera, LayerMask = 1 };
        var defaultView = new GameObject2D([defaultCamera, viewComponent]);
        Children.Add(defaultView);
    }

    /// <summary>
    /// Gets the unique identifier for this scene.
    /// </summary>
    public NodeId Id { get; }

    /// <inheritdoc />
    public ISceneNode? Root => this;

    /// <inheritdoc />
    void ISceneNode.SetRoot(ISceneNode? root)
    {
        if (!ReferenceEquals(root, this))
            throw new InvalidOperationException("A scene is always its own root.");
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

    [Observable(PublicSetter = false)]
    private bool _isInitialized;

    /// <inheritdoc />
    public void Initialize()
    {
        if (_isInitialized)
            return;

        SetIsInitialized(true);
    }

    [Observable(PublicSetter = false)]
    private bool _isActivated;

    /// <inheritdoc />
    public bool CanActivate() => true;

    /// <inheritdoc />
    public void Activate()
    {
        if (_isActivated || !CanActivate())
            return;

        SetIsActivated(true);

        PropertyChanged?.Invoke(nameof(IsActivated));
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    /// <inheritdoc />
    public void Deactivate()
    {
        if (!_isActivated)
            return;

        SetIsActivated(false);
        PropertyChanged?.Invoke(nameof(IsActivated));
    }

    /// <summary>
    /// Adds a node and its existing descendants to the scene lookup and subscriptions.
    /// </summary>
    /// <param name="node">The root node of the subtree to register.</param>
    private void Register(ISceneNode node)
    {
        if (_allNodes.TryGetValue(node.Id, out var registeredNode))
            return;

        if (ReferenceEquals(registeredNode, node))
            return;

        node.SetRoot(this);
        node.Parent ??= this;

        node.Children.ItemAdded += Register;
        node.Children.ItemRemoved += Unregister;

        _allNodes.Add(node.Id, node);

        foreach (var child in node.Children)
            Register(child);
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

        node.SetRoot(null);
        node.Parent = null;

        node.Children.ItemAdded -= Register;
        node.Children.ItemRemoved -= Unregister;

        _allNodes.Remove(node.Id);
    }
}
