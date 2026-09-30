namespace Nexus.Core;

/// <summary>
/// Defines a game object that owns components and participates in the scene lifecycle.
/// </summary>
public interface IGameObject : ISceneNode, IManagedEntity, IObservable
{
    /// <summary>
    /// Gets the observable, read-only collection of components attached to this game object.
    /// </summary>
    IReadOnlyObservableCollection<IComponent> Components { get; }
}
