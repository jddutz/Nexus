using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Core.Performance;

namespace Nexus.GUI;

/// <summary>
/// Tracks active GUI elements and updates their layouts in response to game lifecycle events.
/// </summary>
/// <param name="eventHub">The event hub used to receive game lifecycle events.</param>
/// <param name="windowService">The service used to read the main window size, or <see langword="null"/> in a headless runtime.</param>
/// <param name="diagnostics">Optional diagnostics settings used to enable layout logging.</param>
/// <param name="telemetry">The optional performance telemetry sink.</param>
public sealed class GraphicalUserInterface(
    IEventHub eventHub,
    IWindowService? windowService = null,
    IOptions<DiagnosticsSettings>? diagnostics = null,
    IPerformanceTelemetry? telemetry = null
) : IGraphicalUserInterface
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly IWindowService? _windowService = windowService;
    private readonly bool _diagnosticsEnabled = (diagnostics?.Value ?? new DiagnosticsSettings())
        .GraphicsInstrumentationEnabled;
    private readonly HashSet<Element> _subscribedElements = [];
    private readonly Dictionary<Element, Action<string>> _propertyChangedHandlers = [];
    private IScene? _scene;
    private Element? _focusedElement;
    private Vector2D<int>? _pendingWindowSize;
    private bool _layoutInvalidated;
    private ModalDialog? _modal;
    private InputScope? _modalScope;
    private Element? _savedFocus;

    /// <inheritdoc />
    public ModalDialog StartModalDialog()
    {
        if (_scene is null) throw new InvalidOperationException("A scene must be loaded before opening a dialog.");
        if (_modal is not null) throw new InvalidOperationException("A modal dialog is already open.");
        _savedFocus = _focusedElement;
        SetFocus(null);
        var dialog = new ModalDialog(CloseModalDialog);
        _modal = dialog;
        _modalScope = new InputScope(_eventHub, dialog.ContainsMap);
        _scene.Children.Add(dialog);
        _layoutInvalidated = true;
        return dialog;
    }

    private void CloseModalDialog()
    {
        if (_modal is null) return;
        var dialog = _modal;
        _modal = null;
        SetFocus(null);
        UnsubscribeFromElements(EnumerateElements([dialog]).ToArray());
        dialog.IsVisible = false;
        dialog.Parent?.Children.Remove(dialog);
        _modalScope?.Dispose();
        _modalScope = null;
        var focus = _savedFocus;
        _savedFocus = null;
        if (focus is not null && _subscribedElements.Contains(focus) && focus.IsActivated
            && focus.IsEffectivelyVisible && focus.IsEffectivelyEnabled && focus.CanFocus)
            SetFocus(focus);
        _layoutInvalidated = true;
    }

    /// <inheritdoc />
    public IElement? FocusedElement => _focusedElement;

    /// <summary>
    /// Initializes the graphical user interface.
    /// </summary>
    public void Initialize()
    {
        using var timing = new LoadPerformanceScope(
            telemetry,
            "startup.system.initialize",
            "gui"
        );
        _eventHub.Register(this);
    }

    /// <summary>
    /// Updates the graphical user interface for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time, in seconds, since the previous frame.</param>
    public void Update(double deltaTime)
    {
        foreach (var element in _subscribedElements.ToArray())
            element.InputMap.Update(deltaTime);
        if (_scene is null || _windowService is null)
            return;

        if (_layoutInvalidated)
        {
            _layoutInvalidated = false;
            var size = _pendingWindowSize ?? _windowService.GetMainWindow().Size;
            _pendingWindowSize = null;
            var screenSize = new Vector2D<float>(size.X, size.Y);
            var screenBounds = new Rectangle<float>(Vector2D<float>.Zero, screenSize);
            foreach (var element in EnumerateActiveLayoutRoots(_scene.Children.OfType<IGameObject>()))
                MeasureAndArrange(element, screenSize, screenBounds);
        }

        // Resolve anchors after every view has its final viewport, even without a layout
        // invalidation: target transforms and camera matrices can change each frame.
        foreach (var annotation in _subscribedElements.OfType<ViewAnnotation>().ToArray())
            annotation.Arrange(default);
    }

    /// <inheritdoc />
    public void SetFocus(IElement? element)
    {
        if (
            element is not null
            && (
                element is not Element target
                || !_subscribedElements.Contains(target)
                || !target.IsActivated
                || !target.IsEffectivelyVisible
                || !target.IsEffectivelyEnabled
                || !target.CanFocus
                || (_modal is not null && !_modal.ContainsElement(target))
            )
        )
            throw new ArgumentException(
                "The focused element must be active, registered with the GUI, and focusable.",
                nameof(element)
            );

        var nextElement = (Element?)element;
        if (ReferenceEquals(_focusedElement, nextElement))
            return;

        if (_focusedElement is not null)
            _focusedElement.IsFocused = false;
        _focusedElement = nextElement;
        if (_focusedElement is not null)
            _focusedElement.IsFocused = true;
    }

    /// <inheritdoc />
    public bool MoveFocus(FocusDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var focusableElements = _scene is null
            ? []
            : EnumerateElements(_scene.Children.OfType<IGameObject>())
                .Where(element =>
                    element.IsActivated
                    && _subscribedElements.Contains(element)
                    && element.IsEffectivelyVisible
                    && element.IsEffectivelyEnabled
                    && element.CanFocus
                    && (_modal is null || _modal.ContainsElement(element))
                )
                .ToArray();
        if (focusableElements.Length == 0)
        {
            SetFocus(null);
            return false;
        }

        var currentIndex = Array.IndexOf(focusableElements, _focusedElement);
        var nextIndex = direction switch
        {
            FocusDirection.Next => (currentIndex + 1) % focusableElements.Length,
            FocusDirection.Previous => (currentIndex <= 0 ? focusableElements.Length : currentIndex)
                - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
        SetFocus(focusableElements[nextIndex]);
        return true;
    }

    /// <summary>
    /// Discovers active elements in a loaded scene and queues their initial layouts.
    /// </summary>
    /// <param name="message">The scene-loaded event.</param>
    public void Handle(SceneLoadedEvent message)
    {
        _modal?.Dispose();
        UnsubscribeFromElements(_subscribedElements.ToArray());
        _scene = message.Scene;
        SubscribeToActiveElements(_scene.Children.OfType<IGameObject>());
        _layoutInvalidated = true;
    }

    /// <summary>
    /// Registers active elements in an activated game-object subtree.
    /// </summary>
    /// <param name="message">The game-object activation event.</param>
    public void Handle(GameObjectActivatedEvent message)
    {
        if (message.GameObject.IsActivated)
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
        if (ReferenceEquals(message.GameObject, _modal))
            _modal?.Dispose();
        UnsubscribeFromElements(EnumerateElements([message.GameObject]));
        _layoutInvalidated = true;
    }

    /// <summary>Releases modal input restrictions when the current scene is unloaded.</summary>
    public void Handle(SceneUnloadedEvent message)
    {
        if (!ReferenceEquals(message.Scene, _scene)) return;
        _savedFocus = null;
        _modal?.Dispose();
        UnsubscribeFromElements(_subscribedElements.ToArray());
        _scene = null;
    }

    /// <summary>
    /// Invalidates layout when the main window changes size.
    /// </summary>
    /// <param name="message">The window-resized event.</param>
    public void Handle(WindowResizedEvent message)
    {
        if (_windowService is not null && message.WindowId == _windowService.MainWindowId)
        {
            _pendingWindowSize = message.Size;
            _layoutInvalidated = true;
        }
    }

    /// <summary>
    /// Subscribes to property changes on active elements in the specified hierarchies.
    /// </summary>
    /// <param name="gameObjects">The hierarchy roots to inspect.</param>
    private void SubscribeToActiveElements(IEnumerable<IGameObject> gameObjects)
    {
        foreach (var element in EnumerateElements(gameObjects))
        {
            if (element.IsActivated && _subscribedElements.Add(element))
            {
                Action<string> handler = propertyName =>
                    OnElementPropertyChanged(element, propertyName);
                _propertyChangedHandlers.Add(element, handler);
                element.PropertyChanged += handler;
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
                if (ReferenceEquals(_focusedElement, element))
                    SetFocus(null);

                if (_propertyChangedHandlers.Remove(element, out var handler))
                    element.PropertyChanged -= handler;
                element.InputMap.Unregister(_eventHub);
            }
        }
    }

    /// <summary>
    /// Invalidates layout when an element's requested size changes.
    /// </summary>
    /// <param name="changedElement">The element whose property changed.</param>
    /// <param name="propertyName">The name of the changed property.</param>
    private void OnElementPropertyChanged(Element changedElement, string propertyName)
    {
        if (propertyName is nameof(Element.IsVisible) or nameof(Element.IsEnabled))
        {
            foreach (var affectedElement in EnumerateElements([changedElement]))
            {
                if (affectedElement.IsEffectivelyVisible && affectedElement.IsEffectivelyEnabled)
                    continue;

                affectedElement.InputMap.CancelPointerCapture();
                if (ReferenceEquals(_focusedElement, affectedElement))
                    SetFocus(null);
            }
        }

        if (propertyName == nameof(Element.CanFocus))
        {
            if (!changedElement.CanFocus && ReferenceEquals(_focusedElement, changedElement))
                SetFocus(null);
        }

        if (
            propertyName == nameof(Element.IsVisible)
            || (
                changedElement.IsEffectivelyVisible
                && propertyName
                    is ""
                        or nameof(Element.Width)
                        or nameof(Element.Height)
                        or nameof(Element.Margins)
                        or nameof(TextElement.Text)
                        or nameof(TextElement.Style)
                        or nameof(TextElement.MaximumLines)
                        or nameof(Element.HorizontalAlignment)
                        or nameof(Element.VerticalAlignment)
                        or nameof(ImageElement.SizingMode)
                        or nameof(ImageElement.SourceRegion)
                        or nameof(ImageElement.CustomSize)
                        or nameof(TextButton.Label)
                        or nameof(TextButton.Padding)
            )
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

            foreach (
                var childElement in EnumerateElements(gameObject.Children.OfType<IGameObject>())
            )
                yield return childElement;
        }
    }

    /// <summary>
    /// Measures an active layout root against the screen and arranges it within screen bounds.
    /// </summary>
    /// <param name="element">The root element to lay out.</param>
    /// <param name="screenSize">The current screen size.</param>
    /// <param name="screenBounds">The current screen bounds.</param>
    private void MeasureAndArrange(
        Element element,
        Vector2D<float> screenSize,
        Rectangle<float> screenBounds
    )
    {
        var measuredSize = element.Measure(screenSize);
        element.Arrange(screenBounds);

        if (_diagnosticsEnabled)
        {
            Debug.WriteLine(
                $"GUI Arrange: Element={element.GetType().Name}, MeasuredSize={measuredSize}, "
                    + $"Allocation={screenBounds}, Bounds={element.Bounds}"
            );
        }
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
        if (!gameObject.IsActivated)
            yield break;

        if (gameObject is Element element && layoutRoot is null)
        {
            layoutRoot = element;
            yield return element;
        }

        foreach (var child in gameObject.Children.OfType<IGameObject>())
        foreach (var childRoot in EnumerateActiveLayoutRoots(child, layoutRoot))
            yield return childRoot;
    }
}
