namespace Nexus.Core;

public class GameObject : IGameObject
{
    private readonly List<IGameObject> _children = [];
    private readonly List<IComponent> _components = [];

    public GameObjectId Id { get; }

    public IGameObject? Parent { get; protected set; }

    public IReadOnlyList<IGameObject> Children => _children.AsReadOnly();

    public IReadOnlyList<IComponent> Components => _components.AsReadOnly();

    public bool IsActive { get; internal set; }

    public GameObject()
    {
        Id = GameObjectId.New();
    }

    public GameObject(uint id)
    {
        Id = id;
    }

    public TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent
    {
        var component = Activator.CreateInstance<TComponent>();

        _components.Add(component);

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
            return false;

        if (IsActive)
            OnComponentRemoved(component);

        return _components.Remove(component);
    }

    public void AddChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
            throw new InvalidOperationException("GameObject already belongs to a parent.");

        _children.Add(child);

        (Parent as GameObject)?.Parent = this;

        if (IsActive)
            OnGameObjectAdded(child);
    }

    public bool RemoveChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Contains(child))
            return false;

        if (IsActive)
            OnGameObjectRemoved(child);

        _children.Remove(child);

        (Parent as GameObject)?.Parent = null;

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

    public void Activate() { }

    public void Update(double deltaTime) { }

    public void Deactivate() { }
}
