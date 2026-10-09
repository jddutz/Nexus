using System.Reflection;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Events;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Tests;

/// <summary>
/// Tests GUI element registration and layout invalidation from game lifecycle events.
/// </summary>
public class GraphicalUserInterfaceTests
{
    [Fact]
    public void Modal_blocksSceneAndNewMaps_trapsFocus_andRestoresInput()
    {
        var hub = new EventHub();
        var gui = new GraphicalUserInterface(hub, new TestWindowService(new(800, 600)));
        var background = new Element { CanFocus = true };
        var scene = new Scene { MainCamera = new Nexus.Graphics.Cameras.StaticCamera(), InputMap = new InputMap() };
        scene.Children.Add(background);
        ActivateScene(scene);
        gui.Initialize();
        scene.InputMap.Register(hub);
        hub.Publish(new SceneLoadedEvent(scene));
        hub.Drain();
        gui.SetFocus(background);
        var sceneCalls = 0;
        var backgroundCalls = 0;
        var modalCalls = 0;
        scene.InputMap.OnKeyPressed(KeyEnum.Space).Invoke(() => sceneCalls++);
        background.InputMap.OnKeyPressed(KeyEnum.Space).Invoke(() => backgroundCalls++);
        var dialog = gui.StartModalDialog();
        var button = new Element { CanFocus = true };
        button.InputMap.OnKeyPressed(KeyEnum.Space).Invoke(() => modalCalls++);
        dialog.Content.Children.Add(button);
        ActivateNode(dialog);
        hub.Publish(new GameObjectActivatedEvent(dialog));
        hub.Drain();
        gui.Update(0);
        Assert.Equal(new Rectangle<float>(0, 0, 800, 600), dialog.Bounds);
        Assert.True(gui.MoveFocus(FocusDirection.Next));
        Assert.Same(button, gui.FocusedElement);
        Assert.Throws<ArgumentException>(() => gui.SetFocus(background));
        var lateMap = new InputMap();
        var lateCalls = 0;
        lateMap.OnKeyPressed(KeyEnum.Space).Invoke(() => lateCalls++);
        lateMap.Register(hub);
        scene.InputMap.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        background.InputMap.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        button.InputMap.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        lateMap.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        Assert.Equal(0, sceneCalls);
        Assert.Equal(0, backgroundCalls);
        Assert.Equal(0, lateCalls);
        Assert.Equal(1, modalCalls);
        dialog.Dispose();
        dialog.Dispose();
        Assert.Same(background, gui.FocusedElement);
        scene.InputMap.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        lateMap.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        Assert.Equal(1, sceneCalls);
        Assert.Equal(1, lateCalls);
        Assert.DoesNotContain(dialog, scene.Children);
    }

    [Fact]
    public void LongPress_firesOnce_consumesTap_andCancelsOnMovement()
    {
        var map = new InputMap(hitTest: position => position.X >= 0 && position.X < 100 && position.Y >= 0 && position.Y < 100);
        var mouse = new TestMouse(1);
        var holds = 0;
        var taps = 0;
        map.OnLongPress(() => holds++);
        map.OnMouseButtonReleased(MouseButtonEnum.Left).Invoke(() => taps++);
        map.Handle(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(20, 20)));
        map.Update(0.25);
        Assert.Equal(0, holds);
        map.Update(0.25);
        map.Update(1);
        map.Handle(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(20, 20)));
        Assert.Equal(1, holds);
        Assert.Equal(0, taps);
        map.Handle(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(20, 20)));
        map.Handle(new MouseMovedEvent(mouse, new(40, 20)));
        map.Update(1);
        map.Handle(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(40, 20)));
        Assert.Equal(1, holds);
        Assert.Equal(0, taps);
        map.Handle(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(20, 20)));
        map.Update(0.1);
        map.Handle(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(20, 20)));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void SceneUnload_releasesModalScope_withoutChangingExplicitSuppression()
    {
        var hub = new EventHub();
        var gui = new GraphicalUserInterface(hub);
        var scene = new Scene { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        ActivateScene(scene);
        gui.Handle(new SceneLoadedEvent(scene));
        var suppressed = new InputMap { SuppressSceneInputEvents = true };
        suppressed.Register(hub);
        var normal = new InputMap();
        normal.Register(hub);
        var calls = 0;
        normal.OnKeyPressed(KeyEnum.Space).Invoke(() => calls++);
        suppressed.OnKeyPressed(KeyEnum.Space).Invoke(() => calls += 100);
        var dialog = gui.StartModalDialog();
        gui.Handle(new SceneUnloadedEvent(scene));
        normal.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        suppressed.Handle(new KeyPressedEvent(null!, KeyEnum.Space));
        Assert.Equal(1, calls);
        Assert.True(suppressed.SuppressSceneInputEvents);
        Assert.False(dialog.IsVisible);
        Assert.Throws<InvalidOperationException>(() => gui.StartModalDialog());
    }

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
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
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

    /// <summary>Verifies text changes invalidate layout but texture replacement does not.</summary>
    [Fact]
    public void ContentChanges_invalidateLayoutOnlyWhenTheyCanAffectLayout()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(800, 600)));
        var textElement = new LayoutCountingTextElement();
        var imageElement = new LayoutCountingImageElement { Texture = CreateTexture() };
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(textElement);
        scene.Children.Add(imageElement);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(1, textElement.ArrangeCount);
        Assert.Equal(1, imageElement.ArrangeCount);

        textElement.Text = "Updated";
        gui.Update(0);
        Assert.Equal(2, textElement.ArrangeCount);
        Assert.Equal(2, imageElement.ArrangeCount);

        imageElement.Texture = CreateTexture();
        gui.Update(0);
        Assert.Equal(2, textElement.ArrangeCount);
        Assert.Equal(2, imageElement.ArrangeCount);

        imageElement.IsVisible = false;
        gui.Update(0);
        Assert.Equal(3, imageElement.ArrangeCount);
        imageElement.Texture = CreateTexture();
        gui.Update(0);
        Assert.Equal(3, imageElement.ArrangeCount);

        imageElement.IsVisible = true;
        gui.Update(0);
        Assert.Equal(4, imageElement.ArrangeCount);
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
            arrange: (_, _) => layoutCount++
        );
        var initiallyDiscoveredChildArrangementCount = 0;
        var initiallyDiscoveredChild = new LayoutProbeElement(
            arrange: (_, _) => initiallyDiscoveredChildArrangementCount++
        );
        root.AddChild(initiallyDiscoveredChild);
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(root);
        ActivateScene(scene);
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
        child.Activate();
        eventHub.Publish(new GameObjectActivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(3, layoutCount);
        Assert.Equal(1, childArrangementCount);

        Assert.True(root.RemoveChild(child));
        child.Deactivate();
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
        var mouse = new TestMouse(1);
        var pressCount = 0;
        element.InputMap.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => pressCount++);

        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(5f, 25f)));
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(1, pressCount);

        element.Deactivate();
        eventHub.Publish(new GameObjectDeactivatedEvent(element));
        eventHub.Drain();
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(1, pressCount);

        element.Activate();
        eventHub.Publish(new GameObjectActivatedEvent(element));
        eventHub.Drain();
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(2, pressCount);
    }

    /// <summary>Verifies hidden and disabled ancestors prevent interaction and cancel captured presses.</summary>
    [Fact]
    public void AncestorVisibilityAndEnabledState_gateInputAndClearFocus()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var parent = new LayoutProbeElement();
        var child = new LayoutProbeElement
        {
            CanFocus = true,
            Bounds = new Rectangle<float>(new(10f, 20f), new(100f, 50f)),
        };
        var mouse = new TestMouse(1);
        var pressCount = 0;
        var releaseCount = 0;
        child.InputMap.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => pressCount++);
        child.InputMap.OnMouseButtonReleased(MouseButtonEnum.Left).Invoke(() => releaseCount++);
        parent.AddChild(child);
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(parent);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        gui.SetFocus(child);
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        parent.IsVisible = false;

        Assert.Null(gui.FocusedElement);
        eventHub.Publish(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(1, pressCount);
        Assert.Equal(0, releaseCount);

        parent.IsVisible = true;
        parent.IsEnabled = false;
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(50f, 40f)));
        eventHub.Drain();
        Assert.Equal(1, pressCount);
        Assert.Throws<ArgumentException>(() => gui.SetFocus(child));
    }

    /// <summary>Verifies focus eligibility is distinct from focus assignment.</summary>
    [Fact]
    public void Focus_requiresCanFocusAndRaisesGainedAndLostEvents()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var element = new LayoutProbeElement { CanFocus = true };
        var nonFocusableElement = new LayoutProbeElement();
        var gainedCount = 0;
        var lostCount = 0;
        element.FocusGained += (_, _) => gainedCount++;
        element.FocusLost += (_, _) => lostCount++;
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        scene.Children.Add(nonFocusableElement);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        Assert.Null(gui.FocusedElement);
        Assert.False(element.IsFocused);
        Assert.False(nonFocusableElement.CanFocus);
        Assert.Throws<ArgumentException>(() => gui.SetFocus(nonFocusableElement));

        gui.SetFocus(element);

        Assert.Same(element, gui.FocusedElement);
        Assert.True(element.IsFocused);
        Assert.Equal(1, gainedCount);
        Assert.Equal(0, lostCount);

        gui.SetFocus(null);

        Assert.Null(gui.FocusedElement);
        Assert.False(element.IsFocused);
        Assert.Equal(1, lostCount);
    }

    /// <summary>Verifies focus is cleared when its element becomes ineligible or is deactivated.</summary>
    [Fact]
    public void Focus_clearsWhenElementBecomesIneligibleOrIsDeactivated()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var element = new LayoutProbeElement { CanFocus = true };
        var lostCount = 0;
        element.FocusLost += (_, _) => lostCount++;
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        gui.SetFocus(element);
        element.CanFocus = false;

        Assert.Null(gui.FocusedElement);
        Assert.False(element.IsFocused);
        Assert.Equal(1, lostCount);

        element.CanFocus = true;
        gui.SetFocus(element);
        element.Deactivate();
        eventHub.Publish(new GameObjectDeactivatedEvent(element));
        eventHub.Drain();

        Assert.Null(gui.FocusedElement);
        Assert.False(element.IsFocused);
        Assert.Equal(2, lostCount);
    }

    /// <summary>Verifies focus traversal follows child order, skips ineligible elements, and wraps.</summary>
    [Fact]
    public void MoveFocus_traversesFocusableElementsInChildOrderAndWraps()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var first = new LayoutProbeElement { CanFocus = true };
        var skipped = new LayoutProbeElement();
        var last = new LayoutProbeElement { CanFocus = true };
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(first);
        scene.Children.Add(skipped);
        scene.Children.Add(last);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        Assert.True(gui.MoveFocus(FocusDirection.Next));
        Assert.Same(first, gui.FocusedElement);
        Assert.True(gui.MoveFocus(FocusDirection.Next));
        Assert.Same(last, gui.FocusedElement);
        Assert.True(gui.MoveFocus(FocusDirection.Next));
        Assert.Same(first, gui.FocusedElement);
        Assert.True(gui.MoveFocus(FocusDirection.Previous));
        Assert.Same(last, gui.FocusedElement);
    }

    /// <summary>Verifies focus traversal reports failure when no eligible element remains.</summary>
    [Fact]
    public void MoveFocus_returnsFalseWhenNoFocusableElementsExist()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var element = new LayoutProbeElement();
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        Assert.False(gui.MoveFocus(FocusDirection.Next));
        Assert.Null(gui.FocusedElement);
    }

    /// <summary>Provides a stable identity for hit-tested mouse event tests.</summary>
    private sealed class TestMouse(ulong id) : IMouseInputDevice
    {
        /// <inheritdoc />
        public InputDeviceId Id { get; } = id;

        /// <inheritdoc />
        public string Name => "Test mouse";

        /// <inheritdoc />
        public bool IsConnected => true;

        /// <inheritdoc />
        public Vector2D<float> Position => Vector2D<float>.Zero;

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? Moved
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonPressed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonReleased
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? WheelMoved
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public bool IsButtonDown(MouseButtonEnum button) => false;
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
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        gui.Update(0);

        Assert.Equal(new Vector2D<float>(1920, 1080), measuredSize);
        Assert.Equal(new Rectangle<float>(Vector2D<float>.Zero, new(1920, 1080)), arrangedBounds);
    }

    /// <summary>Verifies changing margins schedules a new layout pass.</summary>
    [Fact]
    public void MarginsChange_invalidatesLayout()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub, new TestWindowService(new(100, 80)));
        var arrangementCount = 0;
        var element = new LayoutProbeElement(
            arrange: (_, _) => arrangementCount++
        );
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();

        gui.Update(0);
        element.Margins = new Margins(2f, 3f, 4f, 5f);
        gui.Update(0);
        element.HorizontalAlignment = AlignHorizontal.Left;
        element.VerticalAlignment = AlignVertical.Top;
        gui.Update(0);

        Assert.Equal(3, arrangementCount);
        Assert.Equal(new Rectangle<float>(2f, 4f, 95f, 71f), element.Bounds);
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
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        ActivateScene(scene);
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
        var scene = new Scene() { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        scene.Children.Add(element);
        element.Activate();
        ActivateScene(scene);
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        gui.Update(0);

        eventHub.Publish(new WindowResizedEvent(windowService.MainWindowId, new(1280, 720)));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(2, arrangedBounds.Count);
        Assert.Equal(new Rectangle<float>(Vector2D<float>.Zero, new(1280, 720)), arrangedBounds[1]);
    }

    /// <summary>Activates a test scene and its hierarchy without assuming scene activation cascades.</summary>
    /// <param name="scene">The scene to activate.</param>
    private static void ActivateScene(Scene scene)
    {
        scene.Initialize();
        scene.Activate();
        foreach (var child in scene.Children)
            ActivateNode(child);
    }

    /// <summary>Initializes and activates a node and its descendants in parent-first order.</summary>
    /// <param name="node">The subtree root to activate.</param>
    private static void ActivateNode(ISceneNode node)
    {
        if (node is IGameObject gameObject)
        {
            gameObject.Initialize();
            gameObject.Activate();
        }

        foreach (var child in node.Children)
            ActivateNode(child);
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

    /// <summary>Counts arranged text layout passes.</summary>
    private sealed class LayoutCountingTextElement : TextElement
    {
        /// <summary>Gets the number of completed arrangement calls.</summary>
        public int ArrangeCount { get; private set; }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            ArrangeCount++;
            base.Arrange(bounds);
        }
    }

    /// <summary>Counts arranged image layout passes.</summary>
    private sealed class LayoutCountingImageElement : ImageElement
    {
        /// <summary>Gets the number of completed arrangement calls.</summary>
        public int ArrangeCount { get; private set; }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            ArrangeCount++;
            base.Arrange(bounds);
        }
    }

    /// <summary>Creates a small texture for image layout tests.</summary>
    /// <returns>The in-memory texture.</returns>
    private static Texture CreateTexture() =>
        new("gui-layout-tests", 2, 2, [Colors.White, Colors.White, Colors.White, Colors.White]);

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
