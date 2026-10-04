namespace Nexus.Game;

/// <summary>
/// Loads scenes by name.
/// </summary>
public interface ISceneRegistry
{
    /// <summary>
    /// Registers a factory for the specified scene name.
    /// </summary>
    /// <param name="sceneName">The name associated with the factory.</param>
    /// <param name="factory">Creates the scene when it is loaded.</param>
    /// <exception cref="ArgumentException">A factory is already registered for the identifier.</exception>
    void Register(string sceneName, Func<IScene> factory);

    /// <summary>
    /// Loads the scene with the specified name.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    /// <returns>The loaded scene, or <see langword="null"/> when it cannot be loaded.</returns>
    /// <exception cref="InvalidOperationException">The registered factory returns <see langword="null"/>.</exception>
    IScene? Load(string sceneName);

}
