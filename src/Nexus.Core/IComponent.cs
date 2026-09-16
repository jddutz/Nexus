namespace Nexus.Core;

public interface IComponent
{
    /// <summary>
    /// Gets the identifier of the game object that owns this component.
    /// </summary>
    GameObjectId GameObjectId { get; }

    /// <summary>
    /// Gets the game model that owns this component's game object.
    /// </summary>
    IGameModel? GameModel { get; }

    /// <summary>
    /// Associates this component with a game object and its game model.
    /// </summary>
    /// <param name="gameObjectId">The identifier of the owning game object.</param>
    /// <param name="gameModel">The game model that owns the game object.</param>
    void SetGameObject(GameObjectId gameObjectId, IGameModel? gameModel);

    /// <summary>
    /// Gets the unique identifier for this component.
    /// </summary>
    ComponentId Id { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this component is activated.
    /// </summary>
    bool IsActivated { get; set; }
}
