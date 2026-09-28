using System.Reflection;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Events;
using Nexus.GUI;
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
        var element = new Element(
            arrange: (currentElement, bounds) =>
            {
                layoutCount++;
                ArrangementRules.Default(currentElement, bounds);
            }
        );
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
        var root = new Element(
            arrange: (element, bounds) =>
            {
                layoutCount++;
                ArrangementRules.Default(element, bounds);
            }
        );
        var initiallyDiscoveredChildArrangementCount = 0;
        var initiallyDiscoveredChild = new Element(
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

        var child = new Element(arrange: (_, _) => childArrangementCount++);
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
    /// Verifies the first layout uses the current screen size and full screen bounds.
    /// </summary>
    [Fact]
    public void InitialLayout_measuresAndArrangesAgainstScreen()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(1920, 1080)));
        Vector2D<float>? measuredSize = null;
        Rectangle<float>? arrangedBounds = null;
        var element = new Element(
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
    /// Verifies default arrangement traverses active non-Element objects and skips inactive branches.
    /// </summary>
    [Fact]
    public void DefaultArrangement_traversesActiveOrdinaryGameObjects()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(640, 480)));
        var arrangementOrder = new List<string>();
        var inactiveArrangementCount = 0;
        var root = new Element(
            arrange: (element, bounds) =>
            {
                arrangementOrder.Add("root");
                ArrangementRules.Default(element, bounds);
            }
        );
        var ordinaryChild = new GameObject();
        var nested = new Element(
            arrange: (element, bounds) =>
            {
                arrangementOrder.Add("nested");
                ArrangementRules.Default(element, bounds);
            }
        );
        var nestedChild = new Element(arrange: (_, _) => arrangementOrder.Add("nested child"));
        var inactiveChild = new Element(arrange: (_, _) => inactiveArrangementCount++);
        var inactiveOrdinaryChild = new GameObject();
        var secondRoot = new Element(arrange: (_, _) => arrangementOrder.Add("second root"));
        nested.AddChild(nestedChild);
        ordinaryChild.AddChild(nested);
        inactiveOrdinaryChild.AddChild(inactiveChild);
        root.AddChild(ordinaryChild);
        root.AddChild(inactiveOrdinaryChild);
        var scene = new Scene();
        scene.AddChild(root);
        scene.AddChild(secondRoot);
        scene.Activate();
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        inactiveOrdinaryChild.Deactivate();
        eventHub.Publish(new GameObjectDeactivatedEvent(inactiveOrdinaryChild));
        eventHub.Drain();

        gui.Update(0);

        Assert.Equal(new[] { "root", "nested", "nested child", "second root" }, arrangementOrder);
        Assert.Equal(0, inactiveArrangementCount);
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
        var element = new Element(
            arrange: (currentElement, bounds) =>
            {
                arrangementCount++;
                ArrangementRules.Default(currentElement, bounds);
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
        var element = new Element(arrange: (_, bounds) => arrangedBounds.Add(bounds));
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
