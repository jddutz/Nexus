namespace Nexus.Core;

public abstract class Component : IComponent
{
    /// <summary>
    /// Gets the identifier of the game object that owns this component.
    /// </summary>
    public GameObjectId GameObjectId { get; private set; } = GameObjectId.Invalid;

    /// <summary>
    /// Gets the game model that owns this component's game object.
    /// </summary>
    public IGameModel? GameModel { get; private set; }

    /// <inheritdoc/>
    public void SetGameObject(GameObjectId gameObjectId, IGameModel? gameModel)
    {
        GameObjectId = gameObjectId;
        GameModel = gameModel;
    }

    /// <summary>
    /// Gets the unique identifier for this component.
    /// </summary>
    public ComponentId Id { get; } = ComponentId.New();

    /// <summary>
    /// Gets or sets a value indicating whether this component is activated.
    /// </summary>
    public bool IsActivated { get; set; } = false;
}
