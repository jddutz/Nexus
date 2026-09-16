namespace Nexus.Core;

public interface IComponent : INotifyPropertyChanged
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
    /// Associates this component with its owning game object.
    /// </summary>
    /// <param name="gameObject">The owning game object, or <see langword="null"/> when detaching.</param>
    void SetGameObject(IGameObject? gameObject);

    /// <summary>
    /// Gets the unique identifier for this component.
    /// </summary>
    ComponentId Id { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this component is activated.
    /// </summary>
    bool IsActivated { get; set; }

    /// <summary>
    /// Occurs when this component's effective state has changed, whether from one of its own
    /// properties or from a change on its owning game object. Systems should subscribe to this
    /// instead of observing the owning game object directly.
    /// </summary>
    event EventHandler? Changed;
}
