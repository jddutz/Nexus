namespace Nexus.Core;

/// <summary>
/// Creates detached game objects from a reusable composition definition.
/// </summary>
/// <typeparam name="TObject">The type of game object created by the template.</typeparam>
public interface ITemplate<out TObject>
    where TObject : IGameObject
{
    /// <summary>
    /// Creates a new game object and optionally initializes its instance properties.
    /// </summary>
    /// <param name="initializer">An action applied once after the object is fully composed.</param>
    /// <returns>A new, detached game object.</returns>
    TObject Create(Action<TObject>? initializer = null);
}
