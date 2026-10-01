namespace Nexus.Core;

/// <summary>
/// Provides the default implementation of a game object.
/// </summary>
public partial class GameObject : IGameObject
{
    private readonly ObservableCollection<IComponent> _components = [];
    private readonly ObservableCollection<ISceneNode> _children = new(
        ReferenceEqualityComparer.Instance
    );

    [Observable(PublicSetter = true)]
    private ISceneNode? _root;

    [Observable(PublicSetter = false)]
    private bool _isInitialized;

    [Observable(PublicSetter = false)]
    private bool _isActivated;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject"/> class with a generated identifier.
    /// </summary>
    public GameObject()
        : this(NodeId.New(), []) { }

    /// <summary>Initializes a game object with a generated identifier and components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject(IEnumerable<IComponent> components)
        : this(NodeId.New(), components) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject(uint id)
        : this(new NodeId(id), []) { }

    /// <summary>Initializes a game object with the specified identifier and components.</summary>
    /// <param name="id">The identifier for the game object.</param>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject(uint id, IEnumerable<IComponent> components)
        : this(new NodeId(id), components) { }

    /// <summary>Initializes a game object from an identifier and component sequence.</summary>
    /// <param name="id">The identifier for the game object.</param>
    /// <param name="components">The components owned by this game object.</param>
    private GameObject(NodeId id, IEnumerable<IComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        Id = id;
        _components.ItemAdded += OnComponentAdded;
        _components.ItemRemoved += OnComponentRemoved;

        _children.ValidationRules += ValidateChild;
        _children.ItemAdded += OnChildAdded;
        _children.ItemRemoved += OnChildRemoved;

        foreach (var component in components)
            AddComponent(component);
    }

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <summary>
    /// Gets the unique identifier for this game object.
    /// </summary>
    public NodeId Id { get; }

    /// <summary>
    /// Gets the parent game object, if this object is attached to one.
    /// </summary>
    [Observable]
    private ISceneNode? _parent;

    /// <summary>
    /// Gets the child scene nodes attached to this game object.
    /// </summary>
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

        return !_children.Contains(node);
    }

    /// <summary>
    /// Ensures a parent assignment is part of adding this node to the parent's child collection.
    /// </summary>
    /// <param name="value">The proposed parent.</param>
    private void BeforeParentChanges(ISceneNode? value)
    {
        if (value is not null && !value.Children.Contains(this))
            throw new InvalidOperationException(
                "A parent can only be assigned by adding the node to its Children collection."
            );
    }

    /// <summary>
    /// Removes this object from its previous parent's child collection after its parent changes.
    /// </summary>
    /// <param name="previousValue">The parent before the change.</param>
    private void AfterParentChanges(ISceneNode? previousValue)
    {
        previousValue?.Children.Remove(this);
        OnHierarchyChanged();
    }

    /// <summary>
    /// Handles this game object's parent or ancestor chain changing.
    /// </summary>
    protected virtual void OnHierarchyChanged()
    {
        foreach (var child in _children)
            if (child is GameObject gameObject)
                gameObject.OnHierarchyChanged();
    }

    private void OnChildAdded(ISceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.Parent = this;
    }

    private void OnChildRemoved(ISceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (ReferenceEquals(node.Parent, this) && !_children.Contains(node))
            node.Parent = null;
    }

    /// <summary>
    /// Gets the observable, read-only collection of components attached to this game object.
    /// </summary>
    public IReadOnlyObservableCollection<IComponent> Components => _components;

    /// <summary>Occurs after a component is added to this object.</summary>
    public event Action<IComponent>? ComponentAdded;

    /// <summary>Occurs after a component is removed from this object.</summary>
    public event Action<IComponent>? ComponentRemoved;

    /// <summary>
    /// Adds a component to this game object.
    /// </summary>
    /// <param name="component">The component to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The component already has an owner.</exception>
    public void AddComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_components.Contains(component))
            return;
        if (component.Owner is not null)
            throw new InvalidOperationException(
                "A component can only be owned by one game object."
            );

        _components.Add(component);
    }

    /// <summary>Creates and adds a component of the requested type.</summary>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <returns>The added component.</returns>
    public TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent, new()
    {
        var component = new TComponent();
        AddComponent(component);
        return component;
    }

    /// <summary>
    /// Removes a component from this game object.
    /// </summary>
    /// <param name="component">The component to remove.</param>
    /// <returns><see langword="true"/> if the component was removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return _components.Remove(component);
    }

    /// <summary>Removes the first component of the requested type.</summary>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <returns><see langword="true"/> when a component was removed.</returns>
    public bool RemoveComponent<TComponent>()
        where TComponent : class, IComponent
    {
        var component = GetComponent<TComponent>();
        return component is not null && RemoveComponent(component);
    }

    /// <summary>Adds a child node to this object's child collection.</summary>
    /// <param name="child">The child node to add.</param>
    public void AddChild(ISceneNode child) => Children.Add(child);

    /// <summary>Removes a child node from this object's child collection.</summary>
    /// <param name="child">The child node to remove.</param>
    /// <returns><see langword="true"/> when the child was removed.</returns>
    public bool RemoveChild(ISceneNode child) => Children.Remove(child);

    /// <summary>Gets the first owned component of the requested type.</summary>
    /// <typeparam name="TComponent">The component type to find.</typeparam>
    /// <returns>The matching component, or <see langword="null"/>.</returns>
    public TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent => Components.OfType<TComponent>().FirstOrDefault();

    /// <inheritdoc />
    public virtual void Initialize()
    {
        foreach (var component in _components)
        {
            if (!component.IsInitialized)
                component.Initialize();
        }

        foreach (var child in _children)
            if (child is GameObject gameObject && !gameObject.IsInitialized)
                gameObject.Initialize();

        IsInitialized = true;
    }

    /// <inheritdoc />
    public virtual bool CanActivate() => true;

    /// <inheritdoc />
    public virtual void Activate()
    {
        if (IsActivated || !CanActivate())
            return;

        IsActivated = true;
        foreach (var component in _components)
        {
            component.Activate();
            ComponentAdded?.Invoke(component);
        }
    }

    /// <inheritdoc />
    public virtual void Update(double deltaTime)
    {
        // Intentionally a no-op
    }

    /// <inheritdoc />
    public virtual void Deactivate()
    {
        IsActivated = false;
    }

    /// <summary>
    /// Invokes the lifecycle callback corresponding to an activation-state transition.
    /// </summary>
    /// <param name="previousValue">The activation state before the change.</param>
    private void AfterIsActivatedChanges(bool previousValue)
    {
        if (previousValue)
            OnDeactivated();
        else
            OnActivated();
    }

    /// <summary>
    /// Handles this object becoming active.
    /// </summary>
    protected virtual void OnActivated() { }

    /// <summary>
    /// Handles this object becoming inactive.
    /// </summary>
    protected virtual void OnDeactivated() { }

    /// <summary>
    /// Assigns ownership and initializes a newly added component.
    /// </summary>
    /// <param name="component">The component added to the collection.</param>
    private void OnComponentAdded(IComponent component)
    {
        component.Owner = this;
        if (!component.IsInitialized)
            component.Initialize();
        ComponentAdded?.Invoke(component);
    }

    /// <summary>
    /// Clears ownership of a component removed from this game object.
    /// </summary>
    /// <param name="component">The component removed from the collection.</param>
    private void OnComponentRemoved(IComponent component)
    {
        if (ReferenceEquals(component.Owner, this) && !_components.Contains(component))
            component.Owner = null;
        ComponentRemoved?.Invoke(component);
    }
}
