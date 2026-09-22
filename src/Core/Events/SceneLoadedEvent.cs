namespace Nexus.Core.Events;

/// <summary>
/// Represents a scene becoming the current scene.
/// </summary>
/// <param name="scene">The scene that became current.</param>
public class SceneLoadedEvent(IScene scene) : IEvent
{
    /// <summary>
    /// Gets the scene that became current.
    /// </summary>
    public IScene Scene { get; } = scene;
}