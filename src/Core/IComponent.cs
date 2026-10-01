namespace Nexus.Core;

/// <summary>
/// Defines a component that can be associated with a game object and participate in its lifecycle.
/// </summary>
public interface IComponent : IManagedEntity, IObservable
{
    /// <summary>
    /// Gets the concise editor-facing caption used to identify this component in component lists
    /// and inspectors. This is not intended to be player-facing text.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the unique identifier for this component.
    /// </summary>
    ComponentId Id { get; }

    /// <summary>
    /// Gets the game object that owns this component, or <see langword="null"/> when it is unowned.
    /// </summary>
    IGameObject? Owner { get; set; }

    /// <summary>
    /// Occurs when effective state has changed, whether from one of its own properties
    /// or from a change to an ancestor or owner.
    /// </summary>
    event EventHandler? Modified;
}
