namespace Nexus.Core;

/// <summary>
/// Defines a scene that is the root of a scene-node hierarchy.
/// </summary>
public interface ISceneObjectFactory
{
    /// <summary>
    /// Creates a new scene node via dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of node to be created.</typeparam>
    /// <returns>An instance of type T</returns>
    T CreateSceneNode<T>(params object[] args)
        where T : ISceneNode;
}
