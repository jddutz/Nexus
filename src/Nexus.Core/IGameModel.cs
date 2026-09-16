namespace Nexus.Core;

/// <summary>
/// Provides game object lookup services for the current game model.
/// </summary>
public interface IGameModel
{
    /// <summary>
    /// Gets the game object with the specified identifier.
    /// </summary>
    /// <param name="gameObjectId">The identifier of the game object to retrieve.</param>
    /// <returns>The game object, or <see langword="null"/> when no matching object is registered.</returns>
    IGameObject? GetGameObject(GameObjectId gameObjectId);

    /// <summary>
    /// Registers a game object so it can be retrieved by its identifier.
    /// </summary>
    /// <param name="gameObject">The game object to register.</param>
    void RegisterGameObject(IGameObject gameObject);

    /// <summary>
    /// Removes a game object from identifier-based lookup.
    /// </summary>
    /// <param name="gameObject">The game object to unregister.</param>
    void UnregisterGameObject(IGameObject gameObject);
}
