namespace Nexus.Game;

/// <summary>
/// Provides the default implementation of a game object.
/// </summary>
public class GameObject : ObservableObject, IGameObject
{
    private readonly List<IGameObject> _children = [];
    private readonly List<IComponent> _components = [];
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
    public IReadOnlyList<IComponent> Components => _components.AsReadOnly();

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

    /// <summary>
    /// Occurs when a component is added to this game object or one of its descendants.
    /// </summary>
    public event Action<IComponent>? ComponentAdded;

    /// <summary>
    /// Occurs when a component is removed from this game object or one of its descendants.
    /// </summary>
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
    {
        Id = GameObjectId.New();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject(uint id)
    {
        Id = id;
    }

    /// <summary>
    /// Creates and adds a component of the specified type.
    /// </summary>
    /// <typeparam name="TComponent">The type of component to add.</typeparam>
    /// <returns>The added component.</returns>
    public TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent
    {
        var component = Activator.CreateInstance<TComponent>();
        AddComponent(component);
        return component;
    }

    /// <inheritdoc/>
    public void AddComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_components.Contains(component))
            return;

        if (component.GameObjectId != GameObjectId.Invalid)
        {
            var previousOwner = component.GameModel?.GetGameObject(component.GameObjectId);
            previousOwner?.RemoveComponent(component);
        }

        _components.Add(component);
        component.SetGameObject(this);
        if (IsActive)
            ComponentAdded?.Invoke(component);
    }

    /// <summary>
    /// Gets the first component of the specified type.
    /// </summary>
    /// <typeparam name="TComponent">The type of component to get.</typeparam>
    /// <returns>The component, or <see langword="null"/> when no matching component is attached.</returns>
    public TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent => _components.OfType<TComponent>().FirstOrDefault();

    /// <summary>
    /// Removes the first component of the specified type.
    /// </summary>
    /// <typeparam name="TComponent">The type of component to remove.</typeparam>
    /// <returns><see langword="true"/> when a component was removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveComponent<TComponent>()
        where TComponent : class, IComponent
    {
        var component = GetComponent<TComponent>();
        return component is not null && RemoveComponent(component);
    }

    /// <inheritdoc/>
    public bool RemoveComponent(IComponent component)
    {
        if (!_components.Remove(component))
            return false;

        if (IsActive)
            ComponentRemoved?.Invoke(component);

        component.SetGameObject(null);
        return true;
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
            if (GameModel is not null)
                gameObject.SetGameModel(GameModel);
        }

        if (IsActive)
        {
            ChildAdded?.Invoke(child);
            child.Activate();
        }
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
            gameObject.Parent = null;

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
    public void Update(double deltaTime) { }

    /// <inheritdoc/>
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        foreach (var component in _components)
            ComponentAdded?.Invoke(component);

        foreach (var child in _children)
            child.Activate();
    }

    /// <inheritdoc/>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        foreach (var child in _children)
            child.Deactivate();

        foreach (var component in _components)
            ComponentRemoved?.Invoke(component);

        IsActive = false;
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