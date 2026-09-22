namespace Nexus.Core;

public interface IGameObject : INotifyPropertyChanged
{
    GameObjectId Id { get; }

    IGameObject? Parent { get; }

    IReadOnlyList<IGameObject> Children { get; }
    IReadOnlyList<IComponent> Components { get; }

    /// <summary>
    /// Gets the game model that owns this game object.
    /// </summary>
    IGameModel? GameModel { get; }

    /// <summary>
    /// Associates this game object with a game model.
    /// </summary>
    /// <param name="gameModel">The game model that owns this game object.</param>
    void SetGameModel(IGameModel gameModel);

    bool IsActive { get; }

    TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent;

    /// <summary>
    /// Attaches an existing component to this game object.
    /// </summary>
    /// <param name="component">The component to attach.</param>
    void AddComponent(IComponent component);

    TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent;

    /// <summary>
    /// Removes the specified component from this game object.
    /// </summary>
    /// <param name="component">The component to remove.</param>
    /// <returns><see langword="true"/> when a component was removed; otherwise, <see langword="false"/>.</returns>
    bool RemoveComponent(IComponent component);

    bool RemoveComponent<TComponent>()
        where TComponent : class, IComponent;

    event Action<IComponent>? ComponentAdded;
    event Action<IComponent>? ComponentRemoved;

    void AddChild(IGameObject child);
    bool RemoveChild(IGameObject child);

    /// <summary>
    /// Creates a new child game object of the specified type and adds it to this game object.
    /// </summary>
    /// <typeparam name="TChild">The type of game object to create.</typeparam>
    /// <returns>The created child game object.</returns>
    TChild CreateChild<TChild>()
        where TChild : IGameObject, new();

    event Action<IGameObject>? ChildAdded;
    event Action<IGameObject>? ChildRemoved;
    void Activate();

    void Update(double deltaTime);
    void Deactivate();
}
