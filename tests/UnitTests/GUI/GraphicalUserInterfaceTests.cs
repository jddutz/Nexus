using System.Reflection;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Events;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Events;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Tests;

/// <summary>
/// Tests GUI element registration and layout invalidation from game lifecycle events.
/// </summary>
public class GraphicalUserInterfaceTests
{
    /// <summary>
    /// Verifies changing an element's requested size invalidates layout.
    /// </summary>
    [Fact]
    public void WidthAndHeightChanges_invalidateLayout()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(800, 600)));
        var layoutCount = 0;
        var element = new LayoutProbeElement(arrange: (_, _) => layoutCount++);
        var scene = new Scene();
        scene.AddChild(element);
        scene.Activate();
        gui.Initialize();

        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        gui.Update(0);

        element.Width = 100;
        gui.Update(0);
        Assert.Equal(2, layoutCount);

        element.Height = 50;
        gui.Update(0);
        Assert.Equal(3, layoutCount);
    }

    /// <summary>
    /// Verifies scene discovery and activation/deactivation events update the affected layout.
    /// </summary>
    [Fact]
    public void LifecycleEvents_discoverRegisterAndRemoveActiveElements()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(800, 600)));
        var layoutCount = 0;
        var childArrangementCount = 0;
        var root = new LayoutProbeElement(
            arrange: (element, bounds) =>
            {
                layoutCount++;
                foreach (
                    var child in element.Children.OfType<Element>().Where(child => child.IsActive)
                )
                    child.Arrange(bounds);
            }
        );
        var initiallyDiscoveredChildArrangementCount = 0;
        var initiallyDiscoveredChild = new LayoutProbeElement(
            arrange: (_, _) => initiallyDiscoveredChildArrangementCount++
        );
        root.AddChild(initiallyDiscoveredChild);
        var scene = new Scene();
        scene.AddChild(root);
        scene.Activate();
        gui.Initialize();

        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(1, layoutCount);
        Assert.Equal(1, initiallyDiscoveredChildArrangementCount);

        Assert.True(root.RemoveChild(initiallyDiscoveredChild));
        eventHub.Publish(new GameObjectDeactivatedEvent(initiallyDiscoveredChild));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(2, layoutCount);
        Assert.Equal(1, initiallyDiscoveredChildArrangementCount);

        var child = new LayoutProbeElement(arrange: (_, _) => childArrangementCount++);
        root.AddChild(child);
        eventHub.Publish(new GameObjectActivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(3, layoutCount);
        Assert.Equal(1, childArrangementCount);

        Assert.True(root.RemoveChild(child));
        eventHub.Publish(new GameObjectDeactivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(4, layoutCount);
        Assert.Equal(1, childArrangementCount);

        eventHub.Publish(new GameObjectActivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(4, layoutCount);
        Assert.Equal(1, childArrangementCount);
    }

    /// <summary>
    /// Verifies active Element input maps are registered, hit-test mouse input, and unregister on deactivation.
    /// </summary>
    [Fact]
    public void ElementInputMaps_registerByLifecycleAndHitTestMouseInput()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var element = new Element { Bounds = new Rectangle<float>(new(10f, 20f), new(100f, 50f)) };
        var pressCount = 0;
        element.InputMap.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => pressCount++);

        var scene = new Scene();
        scene.AddChild(element);
        scene.Activate();
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        eventHub.Publish(new MouseButtonPressedEvent(null!, MouseButtonEnum.Left, new(5f, 25f)));
        eventHub.Publish(new MouseButtonPressedEvent(null!, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(1, pressCount);

        element.Deactivate();
        eventHub.Publish(new GameObjectDeactivatedEvent(element));
        eventHub.Drain();
        eventHub.Publish(new MouseButtonPressedEvent(null!, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(1, pressCount);

        element.Activate();
        eventHub.Publish(new GameObjectActivatedEvent(element));
        eventHub.Drain();
        eventHub.Publish(new MouseButtonPressedEvent(null!, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(2, pressCount);
    }

    /// <summary>
    /// Verifies the first layout uses the current screen size and full screen bounds.
    /// </summary>
    [Fact]
    public void InitialLayout_measuresAndArrangesAgainstScreen()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(1920, 1080)));
        Vector2D<float>? measuredSize = null;
        Rectangle<float>? arrangedBounds = null;
        var element = new LayoutProbeElement(
            measure: (_, available) =>
            {
                measuredSize = available;
                return new(100, 50);
            },
            arrange: (_, bounds) => arrangedBounds = bounds
        );
        var scene = new Scene();
        scene.AddChild(element);
        scene.Activate();
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        gui.Update(0);

        Assert.Equal(new Vector2D<float>(1920, 1080), measuredSize);
        Assert.Equal(new Rectangle<float>(Vector2D<float>.Zero, new(1920, 1080)), arrangedBounds);
    }

    /// <summary>
    /// Verifies a concrete container chooses how its child element is arranged.
    /// </summary>
    [Fact]
    public void ContainerElement_arrangesItsOwnChildren()
    {
        var child = new LayoutProbeElement();
        var container = new ContainerProbeElement(child);
        var bounds = new Rectangle<float>(2f, 3f, 40f, 20f);

        container.Arrange(bounds);

        Assert.Equal(bounds, container.Bounds);
        Assert.Equal(new Rectangle<float>(2f, 3f, 20f, 20f), child.Bounds);
    }

    /// <summary>
    /// Verifies an invalidation raised during layout remains pending for the next update.
    /// </summary>
    [Fact]
    public void InvalidationDuringLayout_isProcessedOnNextUpdate()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(800, 600)));
        var arrangementCount = 0;
        var element = new LayoutProbeElement(
            arrange: (currentElement, bounds) =>
            {
                arrangementCount++;
                if (arrangementCount == 1)
                    currentElement.Width = 100;
            }
        );
        var scene = new Scene();
        scene.AddChild(element);
        scene.Activate();
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        gui.Update(0);
        gui.Update(0);

        Assert.Equal(2, arrangementCount);
    }

    /// <summary>
    /// Verifies a window resize schedules layout using the updated screen dimensions.
    /// </summary>
    [Fact]
    public void WindowResize_invalidatesLayoutAndUsesNewDimensions()
    {
        var eventHub = new EventHub();
        var windowService = new TestWindowService(new(800, 600));
        var gui = new GraphicalUserInterface(eventHub, windowService);
        var arrangedBounds = new List<Rectangle<float>>();
        var element = new LayoutProbeElement(arrange: (_, bounds) => arrangedBounds.Add(bounds));
        var scene = new Scene();
        scene.AddChild(element);
        scene.Activate();
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        gui.Update(0);

        windowService.SetSize(new(1280, 720));
        eventHub.Publish(new WindowResizedEvent(windowService.MainWindowId, new(1280, 720)));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(2, arrangedBounds.Count);
        Assert.Equal(new Rectangle<float>(Vector2D<float>.Zero, new(1280, 720)), arrangedBounds[1]);
    }

    /// <summary>
    /// Provides a test window service backed by a proxy window exposing a mutable size.
    /// </summary>
    private sealed class TestWindowService : IWindowService
    {
        private readonly IWindow _window;

        /// <summary>
        /// Gets the test main window identifier.
        /// </summary>
        public WindowId MainWindowId => 1;

        /// <summary>
        /// Initializes a test window with the specified size.
        /// </summary>
        /// <param name="size">The initial window size.</param>
        public TestWindowService(Vector2D<int> size)
        {
            _window = DispatchProxy.Create<IWindow, TestWindow>();
            ((TestWindow)(object)_window).Size = size;
        }

        /// <summary>
        /// Gets the test window for the specified identifier.
        /// </summary>
        /// <param name="windowId">The requested window identifier.</param>
        /// <returns>The test window.</returns>
        public IWindow GetWindow(WindowId windowId) => _window;

        /// <summary>
        /// Gets the test main window.
        /// </summary>
        /// <returns>The test window.</returns>
        public IWindow GetMainWindow() => _window;

        /// <summary>
        /// Creates no additional windows in this test service.
        /// </summary>
        /// <param name="settings">The requested window settings.</param>
        /// <returns>This method does not return.</returns>
        public WindowId CreateWindow(WindowSettings settings) => throw new NotSupportedException();

        /// <summary>
        /// Closes no windows in this test service.
        /// </summary>
        /// <param name="windowId">The identifier of the window to close.</param>
        public void CloseWindow(WindowId? windowId = null) => throw new NotSupportedException();

        /// <summary>
        /// Updates the size exposed by the test window.
        /// </summary>
        /// <param name="size">The new window size.</param>
        public void SetSize(Vector2D<int> size) => ((TestWindow)(object)_window).Size = size;
    }

    /// <summary>
    /// Records custom layout observations while retaining Element's default behavior.
    /// </summary>
    private sealed class LayoutProbeElement : Element
    {
        private readonly Action<Element, Rectangle<float>>? _arrange;
        private readonly Func<Element, Vector2D<float>, Vector2D<float>>? _measure;

        /// <summary>
        /// Initializes a layout probe with optional measurement and arrangement observations.
        /// </summary>
        /// <param name="arrange">The action invoked before default arrangement.</param>
        /// <param name="measure">The measurement override, if any.</param>
        public LayoutProbeElement(
            Action<Element, Rectangle<float>>? arrange = null,
            Func<Element, Vector2D<float>, Vector2D<float>>? measure = null
        )
        {
            _arrange = arrange;
            _measure = measure;
        }

        /// <inheritdoc />
        public override Vector2D<float> Measure(Vector2D<float> constraint) =>
            _measure?.Invoke(this, constraint) ?? base.Measure(constraint);

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            _arrange?.Invoke(this, bounds);
            base.Arrange(bounds);
        }
    }

    /// <summary>
    /// Arranges its child into the leading half of its own bounds.
    /// </summary>
    private sealed class ContainerProbeElement : Element
    {
        private readonly Element _child;

        /// <summary>
        /// Initializes the probe container with its child element.
        /// </summary>
        /// <param name="child">The element arranged by this container.</param>
        public ContainerProbeElement(Element child)
        {
            _child = child;
            AddChild(child);
        }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            base.Arrange(bounds);
            _child.Arrange(
                new Rectangle<float>(
                    bounds.Origin,
                    new Vector2D<float>(bounds.Size.X / 2f, bounds.Size.Y)
                )
            );
        }
    }

    /// <summary>
    /// Implements only the size property required by GUI layout tests.
    /// </summary>
    public class TestWindow : DispatchProxy
    {
        /// <summary>
        /// Gets or sets the size returned by the proxy window.
        /// </summary>
        public Vector2D<int> Size { get; set; }

        /// <summary>
        /// Handles calls made against the proxied window interface.
        /// </summary>
        /// <param name="targetMethod">The invoked window method.</param>
        /// <param name="args">The invoked method arguments.</param>
        /// <returns>The requested property value, when supported.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Size" => Size,
                "set_Size" => SetSize(args),
                _ => throw new NotSupportedException(targetMethod?.Name),
            };
        }

        /// <summary>
        /// Stores the size passed to the proxied setter.
        /// </summary>
        /// <param name="args">The setter arguments.</param>
        /// <returns><see langword="null"/>.</returns>
        private object? SetSize(object?[]? args)
        {
            Size = (Vector2D<int>)args![0]!;
            return null;
        }
    }
}
