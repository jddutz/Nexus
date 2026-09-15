namespace Nexus.Core;

public class GameObject : IGameObject
{
    private readonly List<IGameObject> _children = [];
    private readonly List<IComponent> _components = [];
    private readonly ILogger<GameObject>? _logger;

    public GameObjectId Id { get; }

    public IGameObject? Parent { get; protected set; }

    public IReadOnlyList<IGameObject> Children => _children.AsReadOnly();

    public IReadOnlyList<IComponent> Components => _components.AsReadOnly();

    public bool IsActive { get; internal set; }

    public GameObject(ILogger<GameObject>? logger = null)
    {
        _logger = logger;
        Id = GameObjectId.New();
        _logger?.LogDebug("Created game object. GameObjectId={GameObjectId}", Id);
    }

    public GameObject(uint id, ILogger<GameObject>? logger = null)
    {
        _logger = logger;
        Id = id;
        _logger?.LogDebug("Created game object with specified ID. GameObjectId={GameObjectId}", Id);
    }

    public TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent
    {
        var component = Activator.CreateInstance<TComponent>();

        _components.Add(component);

        _logger?.LogDebug(
            "Added component to game object. GameObjectId={GameObjectId}, ComponentType={ComponentType}, "
                + "ComponentCount={ComponentCount}",
            Id,
            typeof(TComponent).Name,
            _components.Count
        );

        if (IsActive)
            OnComponentAdded(component);

        return component;
    }

    public TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent
    {
        return _components.OfType<TComponent>().FirstOrDefault();
    }

    public bool RemoveComponent<TComponent>()
        where TComponent : class, IComponent
    {
        var component = GetComponent<TComponent>();

        if (component is null)
        {
            _logger?.LogDebug(
                "Component removal skipped because the component was not found. "
                    + "GameObjectId={GameObjectId}, ComponentType={ComponentType}",
                Id,
                typeof(TComponent).Name
            );
            return false;
        }

        if (IsActive)
            OnComponentRemoved(component);

        var removed = _components.Remove(component);
        _logger?.LogDebug(
            "Removed component from game object. GameObjectId={GameObjectId}, ComponentType={ComponentType}, "
                + "Removed={Removed}, ComponentCount={ComponentCount}",
            Id,
            typeof(TComponent).Name,
            removed,
            _components.Count
        );
        return removed;
    }

    public void AddChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
            throw new InvalidOperationException("GameObject already belongs to a parent.");

        _children.Add(child);

        _logger?.LogDebug(
            "Added child game object. GameObjectId={GameObjectId}, ChildId={ChildId}, ChildCount={ChildCount}",
            Id,
            child.Id,
            _children.Count
        );

        (Parent as GameObject)?.Parent = this;

        if (IsActive)
            OnGameObjectAdded(child);
    }

    public bool RemoveChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Contains(child))
        {
            _logger?.LogDebug(
                "Child removal skipped because the child was not found. "
                    + "GameObjectId={GameObjectId}, ChildId={ChildId}",
                Id,
                child.Id
            );
            return false;
        }

        if (IsActive)
            OnGameObjectRemoved(child);

        _children.Remove(child);

        (Parent as GameObject)?.Parent = null;

        _logger?.LogDebug(
            "Removed child game object. GameObjectId={GameObjectId}, ChildId={ChildId}, ChildCount={ChildCount}",
            Id,
            child.Id,
            _children.Count
        );

        return true;
    }

    public virtual void OnComponentAdded(IComponent component)
    {
        if (IsActive)
            (Parent as GameObject)?.OnComponentAdded(component);
    }

    public virtual void OnComponentRemoved(IComponent component)
    {
        if (IsActive)
            (Parent as GameObject)?.OnComponentRemoved(component);
    }

    public virtual void OnGameObjectAdded(IGameObject gameObject)
    {
        if (IsActive)
            (Parent as GameObject)?.OnGameObjectAdded(gameObject);
    }

    public virtual void OnGameObjectRemoved(IGameObject gameObject)
    {
        if (IsActive)
            (Parent as GameObject)?.OnGameObjectRemoved(gameObject);
    }

    public void Update(double deltaTime) { }

    public void Activate()
    {
        _logger?.LogDebug(
            "Activating game object. GameObjectId={GameObjectId}, ComponentCount={ComponentCount}, "
                + "ChildCount={ChildCount}",
            Id,
            _components.Count,
            _children.Count
        );

        if (IsActive)
            return;

        IsActive = true;

        foreach (var component in _components)
            OnComponentAdded(component);

        foreach (var child in _children)
            child.Activate();
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        _logger?.LogDebug(
            "Deactivating game object. GameObjectId={GameObjectId}, ComponentCount={ComponentCount}, "
                + "ChildCount={ChildCount}",
            Id,
            _components.Count,
            _children.Count
        );

        foreach (var child in _children)
            child.Deactivate();

        foreach (var component in _components)
            OnComponentRemoved(component);

        IsActive = false;
    }
}
