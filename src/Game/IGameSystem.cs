namespace Nexus.Game;

/// <summary>
/// Defines the lifecycle and update operations for the game system.
/// </summary>
public interface IGameSystem
{
    /// <summary>
    /// Gets the currently selected scene.
    /// </summary>
    IScene? CurrentScene { get; }

    /// <summary>
    /// Gets whether a scene hierarchy is currently loaded.
    /// </summary>
    bool IsSceneLoaded { get; }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Unloads the current scene hierarchy while retaining its current-scene reference.
    /// </summary>
    /// <remarks>
    /// <see cref="IsSceneLoaded"/> becomes <see langword="false"/>. The current-scene reference
    /// remains available while the unloaded scene awaits disposal. This method has no effect
    /// when no scene is loaded.
    /// </remarks>
    void UnloadScene();

    /// <summary>
    /// Loads, initializes, and activates the supplied scene hierarchy.
    /// </summary>
    /// <param name="scene">The scene to load.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scene"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A scene is already loaded.</exception>
    void LoadScene(IScene scene);

    /// <summary>
    /// Updates the game system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);
}
