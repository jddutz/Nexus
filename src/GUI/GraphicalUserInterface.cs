namespace Nexus.GUI;

/// <summary>
/// Tracks active GUI elements and updates their layouts in response to game lifecycle events.
/// </summary>
/// <param name="eventHub">The event hub used to receive game lifecycle events.</param>
/// <param name="windowService">The service used to read the main window size, or <see langword="null"/> in a headless runtime.</param>
public sealed class GraphicalUserInterface(IEventHub eventHub, IWindowService? windowService = null)
    : IGraphicalUserInterface
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly IWindowService? _windowService = windowService;
    private readonly HashSet<Element> _subscribedElements = [];
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
        if (!_layoutInvalidated || _scene is null || _windowService is null)
            return;

        _layoutInvalidated = false;
        var size = _windowService.GetMainWindow().Size;
        var screenSize = new Vector2D<float>(size.X, size.Y);
        var screenBounds = new Rectangle<float>(Vector2D<float>.Zero, screenSize);
        foreach (var element in EnumerateActiveLayoutRoots(_scene.Children))
            MeasureAndArrange(element, screenSize, screenBounds);
    }

    /// <summary>
    /// Discovers active elements in a loaded scene and queues their initial layouts.
    /// </summary>
    /// <param name="message">The scene-loaded event.</param>
    public void Handle(SceneLoadedEvent message)
    {
        UnsubscribeFromElements(_subscribedElements.ToArray());
        _scene = message.Scene;
        SubscribeToActiveElements(_scene.Children);
        _layoutInvalidated = true;
    }

    /// <summary>
    /// Registers active elements in an activated game-object subtree.
    /// </summary>
    /// <param name="message">The game-object activation event.</param>
    public void Handle(GameObjectActivatedEvent message)
    {
        if (message.GameObject.IsActive)
        {
            SubscribeToActiveElements([message.GameObject]);
            _layoutInvalidated = true;
        }
    }

    /// <summary>
    /// Removes elements in a deactivated subtree and invalidates their active parent layouts.
    /// </summary>
    /// <param name="message">The game-object deactivation event.</param>
    public void Handle(GameObjectDeactivatedEvent message)
    {
        UnsubscribeFromElements(EnumerateElements([message.GameObject]));
        _layoutInvalidated = true;
    }

    /// <summary>
    /// Invalidates layout when the main window changes size.
    /// </summary>
    /// <param name="message">The window-resized event.</param>
    public void Handle(WindowResizedEvent message)
    {
        if (_windowService is not null && message.WindowId == _windowService.MainWindowId)
            _layoutInvalidated = true;
    }

    /// <summary>
    /// Subscribes to property changes on active elements in the specified hierarchies.
    /// </summary>
    /// <param name="gameObjects">The hierarchy roots to inspect.</param>
    private void SubscribeToActiveElements(IEnumerable<IGameObject> gameObjects)
    {
        foreach (var element in EnumerateElements(gameObjects))
        {
            if (element.IsActive && _subscribedElements.Add(element))
            {
                element.PropertyChanged += OnElementPropertyChanged;
                element.InputMap.Register(_eventHub);
            }
        }
    }

    /// <summary>
    /// Removes property-change subscriptions from the specified elements.
    /// </summary>
    /// <param name="elements">The elements to unsubscribe.</param>
    private void UnsubscribeFromElements(IEnumerable<Element> elements)
    {
        foreach (var element in elements)
        {
            if (_subscribedElements.Remove(element))
            {
                element.CancelPointerInput();
                element.PropertyChanged -= OnElementPropertyChanged;
                element.InputMap.Unregister(_eventHub);
            }
        }
    }

    /// <summary>
    /// Invalidates layout when an element's requested size changes.
    /// </summary>
    /// <param name="sender">The element whose property changed.</param>
    /// <param name="eventArgs">The property-change details.</param>
    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (
            eventArgs.PropertyName
            is null
                or nameof(Element.Width)
                or nameof(Element.Height)
                or nameof(TextButton.Label)
                or nameof(TextButton.Padding)
                or nameof(TextButton.LabelAlignment)
        )
            _layoutInvalidated = true;
    }

    /// <summary>
    /// Enumerates all elements in the specified game-object hierarchies.
    /// </summary>
    /// <param name="gameObjects">The hierarchy roots to inspect.</param>
    /// <returns>All elements in those hierarchies.</returns>
    private static IEnumerable<Element> EnumerateElements(IEnumerable<IGameObject> gameObjects)
    {
        foreach (var gameObject in gameObjects)
        {
            if (gameObject is Element element)
                yield return element;

            foreach (var childElement in EnumerateElements(gameObject.Children))
                yield return childElement;
        }
    }

    /// <summary>
    /// Measures an active layout root against the screen and arranges it within screen bounds.
    /// </summary>
    /// <param name="element">The root element to lay out.</param>
    /// <param name="screenSize">The current screen size.</param>
    /// <param name="screenBounds">The current screen bounds.</param>
    private static void MeasureAndArrange(
        Element element,
        Vector2D<float> screenSize,
        Rectangle<float> screenBounds
    )
    {
        element.Measure(screenSize);
        element.Arrange(screenBounds);
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
