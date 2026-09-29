namespace Nexus.Core;

public interface IGameObject : INotifyPropertyChanged
{
    GameObjectId Id { get; }

    IGameObject? Parent { get; }

    /// <summary>
    /// Gets the direct children of this game object. Raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// when a child is added or removed.
    /// </summary>
    IReadOnlyList<IGameObject> Children { get; }
    IEnumerable<IComponent> Components { get; }

    /// <summary>
    /// Gets the game model that owns this game object. Raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> when the association changes.
    /// </summary>
    IGameModel? GameModel { get; }

    /// <summary>
    /// Associates this game object with a game model.
    /// </summary>
    /// <param name="gameModel">The game model that owns this game object.</param>
    void SetGameModel(IGameModel gameModel);

    bool IsActive { get; }

    /// <summary>Creates and adds a component of the specified type.</summary>
    /// <typeparam name="TComponent">The component type to create.</typeparam>
    /// <returns>The added component.</returns>
    TComponent AddComponent<TComponent>()
        where TComponent : class, IComponent;

    /// <summary>Attaches an existing component to this game object.</summary>
    /// <param name="component">The component to attach.</param>
    void AddComponent(IComponent component);

    TComponent? GetComponent<TComponent>()
        where TComponent : class, IComponent;

    /// <summary>Removes the first component of the specified type.</summary>
    /// <typeparam name="TComponent">The component type to remove.</typeparam>
    /// <returns>True when a component was removed; otherwise, false.</returns>
    bool RemoveComponent<TComponent>()
        where TComponent : class, IComponent;

    /// <summary>Removes the specified component from this game object.</summary>
    /// <param name="component">The component to remove.</param>
    /// <returns>True when the component was removed; otherwise, false.</returns>
    bool RemoveComponent(IComponent component);

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
