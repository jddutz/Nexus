namespace Nexus.GUI;

using Nexus.Core;
using Nexus.Core.Events;

/// <summary>
/// Tracks active GUI elements and updates their layouts in response to game lifecycle events.
/// </summary>
/// <param name="eventHub">The event hub used to receive game lifecycle events.</param>
public sealed class GraphicalUserInterface(IEventHub eventHub) : IGraphicalUserInterface
{
    private readonly IEventHub _eventHub = eventHub;
    private IScene? _scene;
    private bool _layoutInvalidated;

    /// <summary>
    /// Initializes the graphical user interface.
    /// </summary>
    public void Initialize() => _eventHub.Register(this);

    /// <summary>
    /// Updates the graphical user interface for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time, in seconds, since the previous frame.</param>
    public void Update(double deltaTime)
    {
        if (!_layoutInvalidated || _scene is null)
            return;

        _layoutInvalidated = false;
        foreach (var element in EnumerateActiveLayoutRoots(_scene.Children))
            MeasureAndArrange(element);
    }

    /// <summary>
    /// Discovers active elements in a loaded scene and queues their initial layouts.
    /// </summary>
    /// <param name="message">The scene-loaded event.</param>
    public void Handle(SceneLoadedEvent message)
    {
        _scene = message.Scene;
        _layoutInvalidated = true;
    }

    /// <summary>
    /// Registers active elements in an activated game-object subtree.
    /// </summary>
    /// <param name="message">The game-object activation event.</param>
    public void Handle(GameObjectActivatedEvent message)
    {
        if (message.GameObject.IsActive)
            _layoutInvalidated = true;
    }

    /// <summary>
    /// Removes elements in a deactivated subtree and invalidates their active parent layouts.
    /// </summary>
    /// <param name="message">The game-object deactivation event.</param>
    public void Handle(GameObjectDeactivatedEvent message) => _layoutInvalidated = true;

    /// <summary>
    /// Measures an active layout root and arranges it within its current bounds.
    /// </summary>
    /// <param name="element">The root element to lay out.</param>
    private static void MeasureAndArrange(Element element)
    {
        var bounds = element.Bounds;
        var measuredSize = element.Measure(bounds.Size);
        element.Arrange(new Rectangle<float>(bounds.Origin, measuredSize));
    }

    /// <summary>
    /// Enumerates the active top-level element roots in a game-object hierarchy.
    /// </summary>
    /// <param name="gameObjects">The hierarchy roots to inspect.</param>
    /// <returns>The active top-level elements in those hierarchies.</returns>
    private static IEnumerable<Element> EnumerateActiveLayoutRoots(
        IEnumerable<IGameObject> gameObjects
    )
    {
        foreach (var gameObject in gameObjects)
        foreach (var element in EnumerateActiveLayoutRoots(gameObject, null))
            yield return element;
    }

    /// <summary>
    /// Enumerates active layout roots below one game object.
    /// </summary>
    /// <param name="gameObject">The hierarchy root to inspect.</param>
    /// <param name="layoutRoot">The containing element root, if one was found above.</param>
    /// <returns>Active layout roots found in the hierarchy.</returns>
    private static IEnumerable<Element> EnumerateActiveLayoutRoots(
        IGameObject gameObject,
        Element? layoutRoot
    )
    {
        if (!gameObject.IsActive)
            yield break;

        if (gameObject is Element element && layoutRoot is null)
        {
            layoutRoot = element;
            yield return element;
        }

        foreach (var child in gameObject.Children)
        foreach (var childRoot in EnumerateActiveLayoutRoots(child, layoutRoot))
            yield return childRoot;
    }
}
