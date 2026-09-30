namespace Nexus.Core;

/// <summary>
/// Defines a top-level container for game objects.
/// </summary>
public interface IScene : IManagedEntity, IObservable
{
    /// <summary>
    /// Gets the game objects directly contained by this scene.
    /// </summary>
    IObservableCollection<ISceneNode> GameObjects { get; }

    /// <summary>
    /// Gets a value indicating whether this scene is active.
    /// </summary>
    bool IsActive { get; }
}
