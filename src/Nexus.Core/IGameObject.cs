namespace Nexus.Core;

public interface IGameObject
{
    GameObjectId Id { get; }

    IGameObject? Parent { get; }

    IReadOnlyList<IGameObject> Children { get; }
    IReadOnlyList<IComponent> Components { get; }

    bool IsActive { get; }

    TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent;

    TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent;

    bool RemoveComponent<TComponent>()
        where TComponent : class, IComponent;

    event Action<IComponent>? ComponentAdded;
    event Action<IComponent>? ComponentRemoved;

    void AddChild(IGameObject child);
    bool RemoveChild(IGameObject child);

    event Action<IGameObject>? ChildAdded;
    event Action<IGameObject>? ChildRemoved;
    void Activate();

    void Update(double deltaTime);
    void Deactivate();
}
