namespace Nexus.Core;

/// <summary>
/// Provides the default implementation of a game object.
/// </summary>
public partial class GameObject : IGameObject, IObservable
{
    private readonly ObservableCollection<IComponent> _components = [];
    private readonly ObservableCollection<ISceneNode> _children = [];
    private ISceneNode? _parent;

    [Observable(PublicSetter = false)]
    private bool _isInitialized;

    [Observable(PublicSetter = false)]
    private bool _isActivated;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject"/> class with a generated identifier.
    /// </summary>
    public GameObject()
        : this(NodeId.New(), []) { }

    /// <summary>Initializes a game object with a generated identifier and the specified components.</summary>
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
        _children.ItemAdded += OnChildAdded;
        _children.ItemRemoved += OnChildRemoved;

        foreach (var component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            _components.Add(component);
        }
    }

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <summary>
    /// Gets the unique identifier for this game object.
    /// </summary>
    public NodeId Id { get; }

    /// <summary>
    /// Gets the containing scene, or null when detached.
    /// </summary>
    public IScene? Scene { get; set; }

    /// <summary>
    /// Gets the parent game object, if this object is attached to one.
    /// </summary>
    public ISceneNode? Parent
    {
        get => _parent;
        set
        {
            if (ReferenceEquals(_parent, value))
                return;

            _parent = value;
            PropertyChanged?.Invoke(nameof(Parent));
            OnHierarchyChanged();
        }
    }

    /// <summary>
    /// Gets the child scene nodes attached to this game object.
    /// </summary>
    public IObservableCollection<ISceneNode> Children => _children;

    /// <summary>
    /// Adds a child scene node to this game object.
    /// </summary>
    /// <param name="child">The child scene node to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The child already has a parent.</exception>
    public void AddChild(ISceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
            throw new InvalidOperationException("Scene node already belongs to a parent.");

        _children.Add(child);
    }

    /// <summary>
    /// Removes a child scene node from this game object.
    /// </summary>
    /// <param name="child">The child scene node to remove.</param>
    /// <returns><see langword="true"/> if the child was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    public bool RemoveChild(ISceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _children.Remove(child);
    }

    /// <summary>
    /// Gets the observable, read-only collection of components attached to this game object.
    /// </summary>
    public IReadOnlyObservableCollection<IComponent> Components => _components;

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

    /// <inheritdoc />
    public virtual void Initialize()
    {
        SetIsInitialized(true);
    }

    /// <inheritdoc />
    public virtual bool CanActivate() => true;

    /// <inheritdoc />
    public virtual void Activate()
    {
        var wasActivated = IsActivated;
        SetIsActivated(CanActivate());

        if (!wasActivated && IsActivated)
            OnActivated();
        else if (wasActivated && !IsActivated)
            OnDeactivated();
    }

    /// <inheritdoc />
    public virtual void Update(double deltaTime)
    {
        // Intentionally a no-op
    }

    /// <inheritdoc />
    public virtual void Deactivate()
    {
        if (!IsActivated)
            return;

        SetIsActivated(false);
        OnDeactivated();
    }

    /// <summary>
    /// Sets a field and raises a property-change notification when its value changes.
    /// </summary>
    /// <typeparam name="T">The field's value type.</typeparam>
    /// <param name="field">The field to update.</param>
    /// <param name="value">The value to assign.</param>
    /// <param name="propertyName">The name of the associated property.</param>
    /// <returns><see langword="true"/> if the field changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the changed property.</param>
    protected virtual void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(propertyName ?? string.Empty);
    }

    /// <summary>
    /// Handles a change to this object's parent or ancestor chain.
    /// </summary>
    protected virtual void OnHierarchyChanged() { }

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
        component.SetOwner(this);
        component.Initialize();
    }

    /// <summary>
    /// Clears ownership of a component removed from this game object.
    /// </summary>
    /// <param name="component">The component removed from the collection.</param>
    private void OnComponentRemoved(IComponent component)
    {
        if (ReferenceEquals(component.Owner, this) && !_components.Contains(component))
            component.SetOwner(null);
    }

    /// <summary>
    /// Sets the parent of a newly added child to this game object.
    /// </summary>
    /// <param name="child">The child added to the collection.</param>
    private void OnChildAdded(ISceneNode child)
    {
        child.Parent = this;
    }

    /// <summary>
    /// Clears the parent of a removed child when this game object is still its parent.
    /// </summary>
    /// <param name="child">The child removed from the collection.</param>
    private void OnChildRemoved(ISceneNode child)
    {
        if (ReferenceEquals(child.Parent, this) && !_children.Contains(child))
            child.Parent = null;
    }
}
