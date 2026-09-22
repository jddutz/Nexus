namespace Nexus.Core;

/// <summary>
/// Defines a top-level container for game objects.
/// </summary>
public interface IScene
{
    /// <summary>
    /// Gets the game objects directly contained by this scene.
    /// </summary>
    IReadOnlyList<IGameObject> Children { get; }

    /// <summary>
    /// Gets the game model that owns the game objects in this scene.
    /// </summary>
    IGameModel? GameModel { get; }

    /// <summary>
    /// Gets a value indicating whether this scene is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Occurs when a component is added to a game object in this scene.
    /// </summary>
    event Action<IComponent>? ComponentAdded;

    /// <summary>
    /// Occurs when a component is removed from a game object in this scene.
    /// </summary>
    event Action<IComponent>? ComponentRemoved;

    /// <summary>
    /// Occurs when a game object is added to this scene or one of its descendants.
    /// </summary>
    event Action<IGameObject>? GameObjectAdded;

    /// <summary>
    /// Occurs when a game object is removed from this scene or one of its descendants.
    /// </summary>
    event Action<IGameObject>? GameObjectRemoved;

    /// <summary>
    /// Associates this scene and its game objects with a game model.
    /// </summary>
    /// <param name="gameModel">The game model that owns this scene's game objects.</param>
    void SetGameModel(IGameModel gameModel);

    /// <summary>
    /// Adds a top-level game object to this scene.
    /// </summary>
    /// <param name="child">The game object to add.</param>
    void AddChild(IGameObject child);

    /// <summary>
    /// Removes a top-level game object from this scene.
    /// </summary>
    /// <param name="child">The game object to remove.</param>
    /// <returns><see langword="true"/> when the game object was removed; otherwise, <see langword="false"/>.</returns>
    bool RemoveChild(IGameObject child);

    /// <summary>
    /// Creates and adds a top-level game object of the specified type.
    /// </summary>
    /// <typeparam name="TChild">The type of game object to create.</typeparam>
    /// <returns>The added game object.</returns>
    TChild CreateChild<TChild>()
        where TChild : IGameObject, new();

    /// <summary>
    /// Activates this scene and its game objects.
    /// </summary>
    void Activate();

    /// <summary>
    /// Updates the game objects in this scene.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    /// <summary>
    /// Deactivates this scene and its game objects.
    /// </summary>
    void Deactivate();
}
