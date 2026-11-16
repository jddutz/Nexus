namespace Nexus.GameEngine;

/// <summary>
/// Accepts scene transition requests for processing by the runtime.
/// </summary>
public interface ISceneManager
{
    /// <summary>Requests a transition to a scene using its registered scene name.</summary>
    /// <typeparam name="TScene">The scene type whose name identifies its registration.</typeparam>
    void LoadScene<TScene>()
        where TScene : IScene;

    /// <summary>Requests a transition to a scene by registered identifier.</summary>
    /// <param name="sceneId">The registered identifier of the destination scene.</param>
    /// <exception cref="ArgumentException"><paramref name="sceneId"/> is null, empty, or whitespace.</exception>
    void LoadScene(string sceneId);
}
