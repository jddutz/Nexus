namespace Nexus.Core.Events;

/// <summary>
/// Represents a scene no longer being the current scene.
/// </summary>
/// <param name="scene">The scene that is no longer current.</param>
public class SceneUnloadedEvent(IScene scene) : IEvent
{
    /// <summary>
    /// Gets the scene that is no longer current.
    /// </summary>
    public IScene Scene { get; } = scene;
}