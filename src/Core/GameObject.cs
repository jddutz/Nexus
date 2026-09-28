namespace Nexus.Core;

/// <summary>
/// Provides the default implementation of a game object.
/// </summary>
public class GameObject : ObservableObject, IGameObject
{
    private readonly List<IGameObject> _children = [];
    private readonly IComponent[] _components;
    private bool _isActive;

    /// <summary>
    /// Gets the unique identifier for this game object.
    /// </summary>
    public GameObjectId Id { get; }

    /// <summary>
    /// Gets the parent game object, if this object is attached to one.
    /// </summary>
    public IGameObject? Parent { get; protected set; }

    /// <summary>
    /// Gets the child game objects attached to this game object.
    /// </summary>
    public IReadOnlyList<IGameObject> Children => _children.AsReadOnly();

    /// <summary>
    /// Gets the components attached to this game object.
    /// </summary>
    public IEnumerable<IComponent> Components => EnumerateComponents();

    /// <summary>
    /// Gets the game model that owns this game object.
    /// </summary>
    public IGameModel? GameModel { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether this game object is active.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        internal set => SetProperty(ref _isActive, value);
    }

    /// <summary>Occurs when an attached component becomes active on this object or a descendant.</summary>
    public event Action<IComponent>? ComponentAdded;

    /// <summary>Occurs when an attached component becomes inactive on this object or a descendant.</summary>
    public event Action<IComponent>? ComponentRemoved;

    /// <summary>
    /// Occurs when a child is added to this game object or one of its descendants.
    /// </summary>
    public event Action<IGameObject>? ChildAdded;

    /// <summary>
    /// Occurs when a child is removed from this game object or one of its descendants.
    /// </summary>
    public event Action<IGameObject>? ChildRemoved;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject"/> class with a generated identifier.
    /// </summary>
    public GameObject()
        : this(GameObjectId.New(), []) { }

    /// <summary>Initializes a game object with a generated identifier and the specified components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject(IEnumerable<IComponent> components)
        : this(GameObjectId.New(), components) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject(uint id)
        : this(new GameObjectId(id), []) { }

    /// <summary>Initializes a game object with the specified identifier and components.</summary>
    /// <param name="id">The identifier for the game object.</param>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject(uint id, IEnumerable<IComponent> components)
        : this(new GameObjectId(id), components) { }

    /// <summary>Initializes a game object from a validated identifier and component sequence.</summary>
    /// <param name="id">The identifier for the game object.</param>
    /// <param name="components">The components owned by this game object.</param>
    private GameObject(GameObjectId id, IEnumerable<IComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        Id = id;
        _components = components.ToArray();

        var uniqueComponents = new HashSet<IComponent>(ReferenceEqualityComparer.Instance);
        foreach (var component in _components)
        {
            ArgumentNullException.ThrowIfNull(component);
            if (!uniqueComponents.Add(component))
                throw new ArgumentException(
                    "A component can only be supplied once.",
                    nameof(components)
                );
            if (component.GameObjectId != GameObjectId.Invalid)
                throw new ArgumentException(
                    "A component can only be owned by one game object.",
                    nameof(components)
                );
        }

        foreach (var component in _components)
        {
            component.SetGameObject(this);
            component.Initialize();
        }
    }

    /// <summary>
    /// Gets the first component of the specified type.
    /// </summary>
    /// <typeparam name="TComponent">The type of component to get.</typeparam>
    /// <returns>The component, or <see langword="null"/> when no matching component is attached.</returns>
    public TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent => _components.OfType<TComponent>().FirstOrDefault();

    /// <summary>Enumerates the fixed components without exposing the backing array.</summary>
    /// <returns>The components supplied when this game object was created.</returns>
    private IEnumerable<IComponent> EnumerateComponents()
    {
        foreach (var component in _components)
            yield return component;
    }

    /// <inheritdoc/>
    public void SetGameModel(IGameModel gameModel)
    {
        ArgumentNullException.ThrowIfNull(gameModel);
        if (GameModel == gameModel)
            return;

        GameModel?.UnregisterGameObject(this);
        GameModel = gameModel;
        GameModel.RegisterGameObject(this);
        OnPropertyChanged(nameof(GameModel));

        foreach (var child in _children.OfType<GameObject>())
            child.SetGameModel(gameModel);
    }

    /// <inheritdoc/>
    public void AddChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
            throw new InvalidOperationException("GameObject already belongs to a parent.");

        _children.Add(child);
        ListenToChild(child);

        if (child is GameObject gameObject)
        {
            gameObject.Parent = this;
            gameObject.NotifyHierarchyChanged();
            if (GameModel is not null)
                gameObject.SetGameModel(GameModel);
        }

        if (IsActive)
        {
            ChildAdded?.Invoke(child);
            child.Activate();
        }

        OnPropertyChanged(nameof(Children));
    }

    /// <inheritdoc/>
    public bool RemoveChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Contains(child))
            return false;

        if (IsActive)
        {
            child.Deactivate();
            ChildRemoved?.Invoke(child);
        }

        _children.Remove(child);
        StopListeningToChild(child);
        if (child is GameObject gameObject)
        {
            gameObject.Parent = null;
            gameObject.NotifyHierarchyChanged();
        }

        OnPropertyChanged(nameof(Children));
        return true;
    }

    /// <inheritdoc/>
    public TChild CreateChild<TChild>()
        where TChild : IGameObject, new()
    {
        var child = new TChild();
        AddChild(child);
        return child;
    }

    /// <inheritdoc/>
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        OnActivated();
        foreach (var child in _children)
            child.Activate();

        foreach (var component in _components)
        {
            ComponentAdded?.Invoke(component);
        }
    }

    /// <inheritdoc/>
    public virtual void Update(double deltaTime) { }

    /// <inheritdoc/>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        foreach (var child in _children)
            child.Deactivate();

        foreach (var component in _components)
        {
            ComponentRemoved?.Invoke(component);
        }

        IsActive = false;
        OnDeactivated();
    }

    /// <summary>
    /// Subscribes to events raised by an attached child.
    /// </summary>
    /// <param name="child">The child to observe.</param>
    private void ListenToChild(IGameObject child)
    {
        child.ComponentAdded += OnChildComponentAdded;
        child.ComponentRemoved += OnChildComponentRemoved;
        child.ChildAdded += OnChildAdded;
        child.ChildRemoved += OnChildRemoved;
    }

    /// <summary>
    /// Removes subscriptions to events raised by a detached child.
    /// </summary>
    /// <param name="child">The child to stop observing.</param>
    private void StopListeningToChild(IGameObject child)
    {
        child.ComponentAdded -= OnChildComponentAdded;
        child.ComponentRemoved -= OnChildComponentRemoved;
        child.ChildAdded -= OnChildAdded;
        child.ChildRemoved -= OnChildRemoved;
    }

    /// <summary>
    /// Propagates a component-added notification from a child.
    /// </summary>
    /// <param name="component">The added component.</param>
    private void OnChildComponentAdded(IComponent component)
    {
        if (IsActive)
            ComponentAdded?.Invoke(component);
    }

    /// <summary>
    /// Propagates a component-removed notification from a child.
    /// </summary>
    /// <param name="component">The removed component.</param>
    private void OnChildComponentRemoved(IComponent component)
    {
        if (IsActive)
            ComponentRemoved?.Invoke(component);
    }

    /// <summary>
    /// Notifies this object and its descendants that their parent chain changed.
    /// </summary>
    private void NotifyHierarchyChanged()
    {
        OnHierarchyChanged();

        foreach (var child in _children.OfType<GameObject>())
            child.NotifyHierarchyChanged();
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
    /// Propagates a child-added notification from a descendant.
    /// </summary>
    /// <param name="child">The added child.</param>
    private void OnChildAdded(IGameObject child)
    {
        if (IsActive)
            ChildAdded?.Invoke(child);
    }

    /// <summary>
    /// Propagates a child-removed notification from a descendant.
    /// </summary>
    /// <param name="child">The removed child.</param>
    private void OnChildRemoved(IGameObject child)
    {
        if (IsActive)
            ChildRemoved?.Invoke(child);
    }
}
