using System.Reflection;
using Nexus.Core.Events;
using Nexus.GameEngine;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests viewport configuration and layout synchronization for GUI views.</summary>
public sealed class ViewTests
{
    /// <summary>Verifies construction attaches exactly one renderer to the view.</summary>
    [Fact]
    public void Constructor_attachesSingleViewRenderer()
    {
        var view = new View();

        Assert.Same(view.ViewComponent, Assert.Single(view.Components.OfType<ViewRenderer>()));
        Assert.Same(view.ViewComponent, Assert.Single(view.Components));
        Assert.Equal(RenderLayers.All, view.LayerMask);
        Assert.Equal(RenderLayers.All, view.ViewComponent.LayerMask);
    }

    /// <summary>Verifies public view configuration is propagated to the owned renderer.</summary>
    [Fact]
    public void Configuration_changes_propagateToRenderer()
    {
        var view = new View();
        var camera = new StaticCamera();

        view.Camera = camera;
        view.LayerMask = 0x25;
        view.SortOrder = 3;
        view.RenderOrder = 7;
        view.PreserveDrawOrder = true;
        view.BlendMode = BlendMode.Additive;

        Assert.Same(camera, view.ViewComponent.Camera);
        Assert.Equal(0x25UL, view.ViewComponent.LayerMask);
        Assert.Equal(7, view.ViewComponent.RenderOrder);
        Assert.True(view.ViewComponent.PreserveDrawOrder);
        Assert.Equal(BlendMode.Additive, view.ViewComponent.BlendMode);
        Assert.Equal(3, view.SortOrder);
    }

    /// <summary>Verifies arranged bounds, including their origin, define the renderer viewport.</summary>
    [Fact]
    public void Arrange_updatesClippingRegionFromFinalBounds()
    {
        var view = new View();
        var bounds = new Rectangle<float>(12f, 18f, 80f, 60f);

        view.Arrange(bounds);

        Assert.Equal(bounds, view.Bounds);
        Assert.Equal(new Rectangle<int>(12, 18, 80, 60), view.ViewComponent.ClippingRegion);
        Assert.True(view.ViewComponent.HasExplicitClippingRegion);
    }

    /// <summary>Verifies fractional bounds are rounded inward so the viewport cannot exceed them.</summary>
    [Fact]
    public void Arrange_roundsFractionalViewportBoundsInward()
    {
        var view = new View();

        view.Arrange(new Rectangle<float>(10.25f, 20.75f, 9.5f, 8.5f));

        Assert.Equal(new Rectangle<int>(11, 21, 8, 8), view.ViewComponent.ClippingRegion);
    }

    /// <summary>Verifies the default fit mode preserves the camera aspect ratio in the viewport.</summary>
    [Fact]
    public void Arrange_fitModePreservesCameraAspectRatio()
    {
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new View { Camera = camera };

        view.Arrange(new Rectangle<float>(10f, 20f, 300f, 300f));

        Assert.Equal(new Rectangle<int>(10, 95, 300, 150), view.ViewComponent.ViewportRegion);
        Assert.Equal(new Rectangle<int>(10, 20, 300, 300), view.ViewComponent.ClippingRegion);
    }

    /// <summary>Verifies a zero-area arrangement produces an empty explicit viewport.</summary>
    [Fact]
    public void Arrange_zeroAreaProducesEmptyViewport()
    {
        var view = new View();

        view.Arrange(new Rectangle<float>(10f, 20f, 0f, 0f));

        Assert.Equal(new Rectangle<int>(0, 0, 0, 0), view.ViewComponent.ClippingRegion);
        Assert.True(view.ViewComponent.HasExplicitClippingRegion);
    }

    /// <summary>Verifies arranging the view does not change camera projection dimensions.</summary>
    [Fact]
    public void Arrange_doesNotResizeCamera()
    {
        var camera = new StaticCamera();
        camera.SetViewportSize(640f, 480f);
        var projection = camera.ProjectionMatrix;
        var view = new View { Camera = camera };

        view.Arrange(new Rectangle<float>(30f, 40f, 120f, 90f));

        Assert.Equal(projection, camera.ProjectionMatrix);
    }

    /// <summary>Verifies derived views receive mouse-button data and the projected world ray.</summary>
    [Fact]
    public void MouseButtonPressed_invokesProtectedCallback()
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var gui = new GraphicalUserInterface(
            eventHub,
            new TestGraphicsSystem(),
            inputSystem: inputSystem
        );
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new MouseButtonCallbackView
        {
            Camera = camera,
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        view.Initialize();
        view.Activate();
        gui.Handle(new GameObjectActivatedEvent(view));

        var mouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var position = new Vector2D<float>(200f, 100f);
        inputProxy.RaiseMouseButtonPressed(
            new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, position)
        );

        Assert.Same(mouse, view.PressedMouse);
        Assert.True(view.PressedButton.HasValue);
        Assert.Equal(MouseButtonEnum.Left, view.PressedButton.Value);
        Assert.True(view.PressedPosition.HasValue);
        Assert.Equal(position, view.PressedPosition.Value);
        Assert.True(view.PressedWorldRay.HasValue);
        Assert.Equal(Vector3D<float>.Zero, view.PressedWorldRay.Value.Origin);
        Assert.Equal(-Vector3D<float>.UnitZ, view.PressedWorldRay.Value.Direction);
    }

    /// <summary>Verifies active views publish camera rays for mouse and touch positions.</summary>
    [Fact]
    public void ActiveView_publishesWorldRaysForMouseAndTouchInput()
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var gui = new GraphicalUserInterface(
            eventHub,
            new TestGraphicsSystem(),
            inputSystem: inputSystem
        );
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new View { Camera = camera, SizingMode = ViewSizingMode.Stretch };
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        view.Initialize();
        view.Activate();
        gui.Handle(new GameObjectActivatedEvent(view));

        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        Assert.False(view.PropagateInputEvents);
        inputProxy.RaiseMouseMoved(new MouseMovedEvent(null!, new(200f, 100f)));
        eventHub.Drain();
        Assert.Empty(collector.MouseMoved);

        view.PropagateInputEvents = true;
        inputProxy.RaiseMouseMoved(new MouseMovedEvent(null!, new(200f, 100f)));
        eventHub.Drain();

        var mouseEvent = Assert.Single(collector.MouseMoved);
        Assert.Equal(Vector3D<float>.Zero, mouseEvent.WorldRay.Origin);
        Assert.Equal(-Vector3D<float>.UnitZ, mouseEvent.WorldRay.Direction);

        inputProxy.RaiseTouchStarted(new TouchStartedEvent(null!, 4, new(200f, 100f)));
        Assert.Empty(collector.TouchStarted);
        eventHub.Drain();

        var touchEvent = Assert.Single(collector.TouchStarted);
        Assert.Equal(4, touchEvent.InputEvent.TouchId);
        Assert.Equal(Vector3D<float>.Zero, touchEvent.WorldRay.Origin);

        inputProxy.RaiseMouseMoved(new MouseMovedEvent(null!, new(99f, 100f)));
        eventHub.Drain();
        Assert.Single(collector.MouseMoved);

        view.PropagateInputEvents = false;
        inputProxy.RaiseMouseMoved(new MouseMovedEvent(null!, new(200f, 100f)));
        inputProxy.RaiseTouchStarted(new TouchStartedEvent(null!, 5, new(200f, 100f)));
        eventHub.Drain();
        Assert.Single(collector.MouseMoved);
        Assert.Single(collector.TouchStarted);

        view.PropagateInputEvents = true;
        inputProxy.RaiseMouseMoved(new MouseMovedEvent(null!, new(200f, 100f)));
        eventHub.Drain();
        Assert.Equal(2, collector.MouseMoved.Count);

        view.Deactivate();
        gui.Handle(new GameObjectDeactivatedEvent(view));
        inputProxy.RaiseMouseMoved(new MouseMovedEvent(null!, new(200f, 100f)));
        eventHub.Drain();
        Assert.Equal(2, collector.MouseMoved.Count);
    }

    /// <summary>Verifies each reported touch phase produces a view event on the first drain only.</summary>
    /// <param name="phase">The touch phase to report: start, movement, or end.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReportedTouch_reachesViewOnFirstDrainAndRespectsSubscriptionToggles(int phase)
    {
        var eventHub = new EventHub();
        using var inputSystem = new InputSystem(eventHub);
        inputSystem.Initialize();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new View { Camera = camera, SizingMode = ViewSizingMode.Stretch };
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        view.Initialize();
        view.Activate();
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var position = new Vector2D<float>(200f, 100f);
        var started = new TouchStartedEvent(null!, 4, position);
        var moved = new TouchMovedEvent(null!, 4, position);
        var ended = new TouchEndedEvent(null!, 4, position);
        Action report = phase switch
        {
            0 => () => inputSystem.ReportTouchStarted(started),
            1 => () => inputSystem.ReportTouchMoved(moved),
            _ => () => inputSystem.ReportTouchEnded(ended),
        };

        report();
        view.PropagateInputEvents = true;
        eventHub.Drain();
        Assert.Empty(collector.TouchEvents);

        if (phase != 0)
        {
            inputSystem.ReportTouchStarted(started);
            eventHub.Drain();
            collector.TouchEvents.Clear();
        }

        report();
        Assert.Empty(collector.TouchEvents);
        eventHub.Drain();
        var message = Assert.Single(collector.TouchEvents);
        var ray = phase switch
        {
            0 => Assert.IsType<ViewTouchStartedEvent>(message).WorldRay,
            1 => Assert.IsType<ViewTouchMovedEvent>(message).WorldRay,
            _ => Assert.IsType<ViewTouchEndedEvent>(message).WorldRay,
        };
        Assert.Equal(Vector3D<float>.Zero, ray.Origin);
        Assert.Equal(-Vector3D<float>.UnitZ, ray.Direction);
        eventHub.Drain();
        Assert.Single(collector.TouchEvents);

        view.PropagateInputEvents = false;
        report();
        view.PropagateInputEvents = true;
        eventHub.Drain();
        Assert.Single(collector.TouchEvents);

        if (phase != 0)
        {
            inputSystem.ReportTouchStarted(started);
            eventHub.Drain();
            collector.TouchEvents.RemoveAt(collector.TouchEvents.Count - 1);
        }

        report();
        eventHub.Drain();
        Assert.Equal(2, collector.TouchEvents.Count);

        view.Deactivate();
        gui.Handle(new GameObjectDeactivatedEvent(view));
        report();
        eventHub.Drain();
        Assert.Equal(2, collector.TouchEvents.Count);
    }

    /// <summary>Verifies visible GUI overlays block mouse and touch input even when disabled.</summary>
    /// <param name="overlayEnabled">Whether the covering overlay is enabled.</param>
    /// <param name="touch">Whether to report touch instead of a mouse release.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Overlay_blocksViewPointerInputButLayoutContainersDoNot(bool overlayEnabled, bool touch)
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var view = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
            SortOrder = 100,
        };
        var overlay = new ImageElement
        {
            Texture = BuiltInTextures.Uniform,
            SizingMode = ImageSizingMode.Stretch,
            IsEnabled = overlayEnabled,
            SortOrder = -100,
        };
        var layout = new Element { Bounds = new Rectangle<float>(0f, 0f, 800f, 600f) };
        layout.Children.Add(view);
        layout.Children.Add(overlay);
        layout.Initialize();
        layout.Activate();
        view.Activate();
        overlay.Activate();
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        overlay.Arrange(new Rectangle<float>(180f, 80f, 40f, 40f));
        gui.Handle(new GameObjectActivatedEvent(layout));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        Action<Vector2D<float>> report = touch
            ? position => inputProxy.RaiseTouchStarted(new TouchStartedEvent(null!, 1, position))
            : position => inputProxy.RaiseMouseButtonReleased(
                new MouseButtonReleasedEvent(null!, MouseButtonEnum.Left, position)
            );

        report(new(200f, 100f));
        eventHub.Drain();
        Assert.Empty(collector.TouchEvents);
        Assert.Empty(collector.MouseReleased);

        report(new(260f, 100f));
        eventHub.Drain();
        Assert.Equal(1, collector.TouchEvents.Count + collector.MouseReleased.Count);

        overlay.IsVisible = false;
        report(new(200f, 100f));
        eventHub.Drain();
        Assert.Equal(2, collector.TouchEvents.Count + collector.MouseReleased.Count);

        layout.IsEnabled = false;
        report(new(200f, 100f));
        eventHub.Drain();
        Assert.Equal(2, collector.TouchEvents.Count + collector.MouseReleased.Count);
    }

    /// <summary>Verifies modal and explicit input restrictions also apply to direct view input.</summary>
    [Fact]
    public void ModalAndInputScopes_blockDirectViewInputAndRestoreItOnClose()
    {
        var eventHub = new EventHub();
        using var inputSystem = new InputSystem(eventHub);
        inputSystem.Initialize();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var view = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        var scene = new Scene();
        scene.Children.Add(view);
        scene.Initialize();
        scene.Activate();
        view.Activate();
        view.Arrange(new Rectangle<float>(0f, 0f, 200f, 100f));
        gui.Handle(new SceneLoadedEvent(scene));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var started = new TouchStartedEvent(null!, 1, new(100f, 50f));

        // A newly opened modal blocks immediately, before its activation and layout events.
        using (var dialog = gui.StartModalDialog())
        {
            inputSystem.ReportTouchStarted(started);
            eventHub.Drain();
            Assert.Empty(collector.TouchEvents);
        }

        inputSystem.ReportTouchStarted(started);
        eventHub.Drain();
        Assert.Single(collector.TouchEvents);

        using (var scope = new InputScope(eventHub, _ => false))
        {
            inputSystem.ReportTouchStarted(started);
            eventHub.Drain();
            Assert.Single(collector.TouchEvents);
        }

        view.InputMap.SuppressSceneInputEvents = true;
        inputSystem.ReportTouchStarted(started);
        eventHub.Drain();
        Assert.Single(collector.TouchEvents);

        view.InputMap.SuppressSceneInputEvents = false;
        inputSystem.ReportTouchStarted(started);
        eventHub.Drain();
        Assert.Equal(2, collector.TouchEvents.Count);
    }

    /// <summary>Verifies overlapping views route input to the visually higher view only.</summary>
    [Fact]
    public void OverlappingViews_selectRecipientByRenderOrder()
    {
        var eventHub = new EventHub();
        using var inputSystem = new InputSystem(eventHub);
        inputSystem.Initialize();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var first = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        var secondCamera = new OrthoCamera();
        secondCamera.Translate(new Vector3D<float>(10f, 0f, 0f));
        var second = new View
        {
            Camera = secondCamera,
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        foreach (var view in new[] { first, second })
        {
            view.Initialize();
            view.Activate();
            view.Arrange(new Rectangle<float>(0f, 0f, 200f, 100f));
            gui.Handle(new GameObjectActivatedEvent(view));
        }
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var started = new TouchStartedEvent(null!, 1, new(100f, 50f));

        inputSystem.ReportTouchStarted(started);
        eventHub.Drain();
        Assert.Equal(10f, Assert.Single(collector.TouchStarted).WorldRay.Origin.X);

        first.RenderOrder = 1;
        inputSystem.ReportTouchStarted(started);
        eventHub.Drain();
        Assert.Equal(2, collector.TouchStarted.Count);
        Assert.Equal(0f, collector.TouchStarted[1].WorldRay.Origin.X);

        first.PropagateInputEvents = false;
        inputSystem.ReportTouchStarted(started);
        eventHub.Drain();
        Assert.Equal(2, collector.TouchStarted.Count);
    }

    /// <summary>Verifies mouse interactions terminate outside the viewport or after targeting changes.</summary>
    /// <param name="cancel">Whether to cancel all buttons instead of releasing one button.</param>
    /// <param name="terminationTarget">Whether to end outside, beneath a new overlay, or without a camera.</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void MouseInteraction_deliversTerminationToOriginatingView(bool cancel, int terminationTarget)
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var mouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var otherMouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new View
        {
            Camera = camera,
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Initialize();
        view.Activate();
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var startPosition = new Vector2D<float>(200f, 100f);
        var outsidePosition = new Vector2D<float>(400f, 100f);

        inputProxy.RaiseMouseButtonPressed(new(mouse, MouseButtonEnum.Left, startPosition));
        inputProxy.RaiseMouseButtonPressed(new(mouse, MouseButtonEnum.Right, startPosition));
        inputProxy.RaiseMouseMoved(new(mouse, outsidePosition));
        inputProxy.RaiseMouseCanceled(new(otherMouse, outsidePosition));
        inputProxy.RaiseMouseButtonReleased(new(otherMouse, MouseButtonEnum.Left, outsidePosition));
        eventHub.Drain();
        Assert.Equal(2, collector.MousePressed.Count);
        Assert.Equal(outsidePosition, Assert.Single(collector.MouseMoved).InputEvent.Position);
        Assert.Empty(collector.MouseCanceled);
        Assert.Empty(collector.MouseReleased);

        if (terminationTarget == 1)
        {
            var overlay = new ImageElement
            {
                Texture = BuiltInTextures.Uniform,
                SizingMode = ImageSizingMode.Stretch,
            };
            overlay.Initialize();
            overlay.Activate();
            overlay.Arrange(view.Bounds);
            gui.Handle(new GameObjectActivatedEvent(overlay));
        }
        else if (terminationTarget == 2)
        {
            view.Camera = null;
        }

        var endPosition = terminationTarget == 1 ? startPosition : outsidePosition;
        if (cancel)
            inputProxy.RaiseMouseCanceled(new(mouse, endPosition));
        else
            inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Left, endPosition));
        eventHub.Drain();

        var ray = cancel
            ? Assert.Single(collector.MouseCanceled).WorldRay
            : Assert.Single(collector.MouseReleased).WorldRay;
        var expectedRay = terminationTarget == 2
            ? collector.MousePressed[0].WorldRay
            : camera.ScreenToWorldRay(endPosition - new Vector2D<float>(100f, 50f), 200, 100);
        Assert.Equal(expectedRay.Origin, ray.Origin);
        Assert.Equal(expectedRay.Direction, ray.Direction);

        inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Right, outsidePosition));
        inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Left, outsidePosition));
        inputProxy.RaiseMouseCanceled(new(mouse, outsidePosition));
        eventHub.Drain();
        Assert.Equal(cancel ? 1 : 0, collector.MouseCanceled.Count);
        Assert.Equal(cancel ? 0 : 2, collector.MouseReleased.Count);
    }

    /// <summary>Verifies touch termination ownership is tracked separately per device and finger.</summary>
    [Fact]
    public void TouchInteractions_deliverOutsideEndsOncePerDeviceAndFinger()
    {
        var eventHub = new EventHub();
        using var inputSystem = new InputSystem(eventHub);
        inputSystem.Initialize();
        var firstScreen = DispatchProxy.Create<ITouchScreenInputDevice, TestInputSystem>();
        var secondScreen = DispatchProxy.Create<ITouchScreenInputDevice, TestInputSystem>();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var view = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Initialize();
        view.Activate();
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var inside = new Vector2D<float>(200f, 100f);
        var outside = new Vector2D<float>(400f, 100f);

        inputSystem.ReportTouchStarted(new(firstScreen, 1, inside));
        inputSystem.ReportTouchStarted(new(firstScreen, 2, inside));
        inputSystem.ReportTouchStarted(new(secondScreen, 1, inside));
        eventHub.Drain();
        Assert.Equal(3, collector.TouchStarted.Count);

        inputSystem.ReportTouchMoved(new(firstScreen, 1, outside));
        inputSystem.ReportTouchEnded(new(firstScreen, 3, outside));
        var ended = new TouchEndedEvent(firstScreen, 1, outside);
        inputSystem.ReportTouchEnded(ended);
        inputSystem.ReportTouchEnded(ended);
        eventHub.Drain();
        var endEvent = Assert.Single(collector.TouchEvents.OfType<ViewTouchEndedEvent>());
        Assert.Same(ended, endEvent.InputEvent);
        Assert.Equal(
            outside,
            Assert.Single(collector.TouchEvents.OfType<ViewTouchMovedEvent>()).InputEvent.Position
        );

        inputSystem.ReportTouchEnded(new(secondScreen, 1, outside));
        view.Camera = null;
        inputSystem.ReportTouchEnded(new(firstScreen, 2, outside));
        eventHub.Drain();
        var ends = collector.TouchEvents.OfType<ViewTouchEndedEvent>().ToArray();
        Assert.Equal(3, ends.Length);
        Assert.Same(secondScreen, ends[1].InputEvent.TouchScreen);
        Assert.Same(firstScreen, ends[2].InputEvent.TouchScreen);
        Assert.Equal(2, ends[2].InputEvent.TouchId);
        Assert.Equal(collector.TouchStarted[1].WorldRay.Origin, ends[2].WorldRay.Origin);
    }

    /// <summary>Verifies subscription removal cancels owned interactions once before discarding them.</summary>
    /// <param name="unregister">Whether to deactivate rather than toggle propagation.</param>
    /// <param name="removeCamera">Whether cancellation must use the accepted start rays.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InputSubscriptionRemoval_cancelsRetainedInteractions(bool unregister, bool removeCamera)
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var mouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var screen = DispatchProxy.Create<ITouchScreenInputDevice, TestInputSystem>();
        var otherScreen = DispatchProxy.Create<ITouchScreenInputDevice, TestInputSystem>();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var camera = new OrthoCamera();
        camera.SetSize(100f, 100f);
        var view = new View
        {
            Camera = camera,
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Initialize();
        view.Activate();
        view.Arrange(new Rectangle<float>(0f, 0f, 100f, 100f));
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var inside = new Vector2D<float>(50f, 50f);
        var outside = new Vector2D<float>(200f, 50f);
        inputProxy.RaiseMouseButtonPressed(new(mouse, MouseButtonEnum.Left, inside));
        inputProxy.RaiseMouseButtonPressed(new(mouse, MouseButtonEnum.Right, inside));
        inputProxy.RaiseTouchStarted(new(screen, 1, inside));
        inputProxy.RaiseTouchStarted(new(screen, 2, inside));
        inputProxy.RaiseTouchStarted(new(otherScreen, 1, inside));
        inputProxy.RaiseMouseMoved(new(mouse, outside));
        inputProxy.RaiseTouchMoved(new(screen, 1, outside));
        inputProxy.RaiseTouchEnded(new(screen, 2, outside));
        eventHub.Drain();
        if (removeCamera)
            view.Camera = null;

        if (unregister)
        {
            view.Deactivate();
            gui.Handle(new GameObjectDeactivatedEvent(view));
            view.Activate();
            gui.Handle(new GameObjectActivatedEvent(view));
        }
        else
        {
            view.PropagateInputEvents = false;
            view.PropagateInputEvents = true;
        }

        Assert.Empty(collector.MouseCanceled);
        Assert.Empty(collector.TouchCanceled);
        eventHub.Drain();
        var mouseCancellation = Assert.Single(collector.MouseCanceled);
        Assert.Same(mouse, mouseCancellation.InputEvent.Mouse);
        Assert.Equal(outside, mouseCancellation.InputEvent.Position);
        Assert.Equal(2, collector.TouchCanceled.Count);
        var touchCancellation = Assert.Single(
            collector.TouchCanceled, message => ReferenceEquals(message.TouchScreen, screen)
        );
        Assert.Equal(1, touchCancellation.TouchId);
        Assert.Equal(outside, touchCancellation.Position);
        Assert.Same(otherScreen, collector.TouchCanceled[1].TouchScreen);
        Assert.Equal(1, collector.TouchCanceled[1].TouchId);
        Assert.Equal(inside, collector.TouchCanceled[1].Position);
        var expectedMouseRay = removeCamera
            ? collector.MousePressed[0].WorldRay
            : collector.MouseMoved[0].WorldRay;
        var expectedTouchRay = removeCamera
            ? collector.TouchStarted[0].WorldRay
            : Assert.Single(collector.TouchEvents.OfType<ViewTouchMovedEvent>()).WorldRay;
        Assert.Equal(expectedMouseRay.Origin, mouseCancellation.WorldRay.Origin);
        Assert.Equal(expectedMouseRay.Direction, mouseCancellation.WorldRay.Direction);
        Assert.Equal(expectedTouchRay.Origin, touchCancellation.WorldRay.Origin);
        Assert.Equal(expectedTouchRay.Direction, touchCancellation.WorldRay.Direction);

        inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Left, outside));
        inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Right, outside));
        inputProxy.RaiseTouchEnded(new(screen, 1, outside));
        inputProxy.RaiseTouchEnded(new(otherScreen, 1, outside));
        view.PropagateInputEvents = false;
        gui.Handle(new GameObjectDeactivatedEvent(view));
        eventHub.Drain();
        Assert.Single(collector.MouseCanceled);
        Assert.Equal(2, collector.TouchCanceled.Count);
        Assert.Empty(collector.MouseReleased);
        Assert.Single(collector.TouchEvents.OfType<ViewTouchEndedEvent>());
    }

    /// <summary>Verifies disconnection exits and cancels only the disconnected mouse without waiting for a drain.</summary>
    [Fact]
    public void MouseDisconnection_cancelsCapturedButtonsOnceAndPreservesOtherDevices()
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var mouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var otherMouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var hoverMouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var camera = new OrthoCamera();
        camera.SetSize(100f, 100f);
        var view = new View
        {
            Camera = camera,
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Initialize();
        view.Activate();
        view.Arrange(new Rectangle<float>(0f, 0f, 100f, 100f));
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var inside = new Vector2D<float>(50f, 50f);
        var outside = new Vector2D<float>(200f, 50f);
        inputProxy.RaiseMouseButtonPressed(new(mouse, MouseButtonEnum.Left, inside));
        inputProxy.RaiseMouseButtonPressed(new(mouse, MouseButtonEnum.Right, inside));
        inputProxy.RaiseMouseButtonPressed(new(otherMouse, MouseButtonEnum.Left, inside));
        inputProxy.RaiseMouseMoved(new(hoverMouse, inside));
        inputProxy.RaiseMouseDisconnected(new(hoverMouse, inside));
        eventHub.Drain();
        Assert.Empty(collector.MouseCanceled);
        Assert.Same(hoverMouse, Assert.Single(collector.MouseExited).InputEvent.Mouse);

        inputProxy.RaiseMouseDisconnected(new(mouse, outside));
        inputProxy.RaiseMouseDisconnected(new(mouse, inside));
        inputProxy.RaiseMouseMoved(new(mouse, outside));
        inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Left, outside));
        inputProxy.RaiseMouseButtonReleased(new(mouse, MouseButtonEnum.Right, outside));
        inputProxy.RaiseMouseMoved(new(otherMouse, outside));
        inputProxy.RaiseMouseButtonReleased(new(otherMouse, MouseButtonEnum.Left, outside));
        Assert.Empty(collector.MouseCanceled);
        eventHub.Drain();
        var cancellation = Assert.Single(collector.MouseCanceled);
        Assert.Same(mouse, cancellation.InputEvent.Mouse);
        Assert.Equal(outside, cancellation.InputEvent.Position);
        Assert.Same(otherMouse, Assert.Single(collector.MouseReleased).InputEvent.Mouse);
        Assert.Equal(2, collector.MouseMoved.Count);
        Assert.Same(otherMouse, collector.MouseMoved[1].InputEvent.Mouse);
        Assert.Equal(
            view.Camera.ScreenToWorldRay(outside, 100, 100).Origin,
            cancellation.WorldRay.Origin
        );
        Assert.Equal(
            view.Camera.ScreenToWorldRay(outside, 100, 100).Direction,
            cancellation.WorldRay.Direction
        );
    }

    /// <summary>Verifies mouse hover transitions are per-device and exits preserve drag capture.</summary>
    [Fact]
    public void MouseTransitions_distinguishHoverExitFromCapturedMovementAndRelease()
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var firstMouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var secondMouse = DispatchProxy.Create<IMouseInputDevice, TestInputSystem>();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var view = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Initialize();
        view.Activate();
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var inside = new Vector2D<float>(200f, 100f);
        var outside = new Vector2D<float>(400f, 100f);

        inputProxy.RaiseMouseMoved(new(firstMouse, inside));
        inputProxy.RaiseMouseButtonPressed(new(firstMouse, MouseButtonEnum.Left, inside));
        inputProxy.RaiseMouseMoved(new(firstMouse, outside));
        inputProxy.RaiseMouseButtonReleased(new(firstMouse, MouseButtonEnum.Left, outside));
        inputProxy.RaiseMouseMoved(new(firstMouse, outside));
        eventHub.Drain();
        Assert.Equal(
            [
                typeof(ViewMouseEnteredEvent),
                typeof(ViewMouseMovedEvent),
                typeof(ViewMouseButtonPressedEvent),
                typeof(ViewMouseExitedEvent),
                typeof(ViewMouseMovedEvent),
                typeof(ViewMouseButtonReleasedEvent),
            ],
            collector.MouseEvents.Select(message => message.GetType())
        );
        Assert.Empty(collector.MouseCanceled);
        Assert.Same(firstMouse, Assert.Single(collector.MouseExited).InputEvent.Mouse);

        inputProxy.RaiseMouseMoved(new(firstMouse, inside));
        inputProxy.RaiseMouseMoved(new(secondMouse, inside));
        inputProxy.RaiseMouseMoved(new(secondMouse, inside));
        inputProxy.RaiseMouseMoved(new(secondMouse, outside));
        eventHub.Drain();
        Assert.Equal(3, collector.MouseEntered.Count);
        Assert.Equal(2, collector.MouseExited.Count);
        Assert.Same(secondMouse, collector.MouseExited[1].InputEvent.Mouse);

        var overlay = new ImageElement
        {
            Texture = BuiltInTextures.Uniform,
            SizingMode = ImageSizingMode.Stretch,
        };
        overlay.Initialize();
        overlay.Activate();
        overlay.Arrange(view.Bounds);
        gui.Handle(new GameObjectActivatedEvent(overlay));
        eventHub.Drain();
        Assert.Equal(3, collector.MouseExited.Count);
        Assert.Same(firstMouse, collector.MouseExited[2].InputEvent.Mouse);
        Assert.Empty(collector.MouseCanceled);
    }

    /// <summary>Verifies unavailable views emit one exit per inside pointer without cancellation.</summary>
    /// <param name="transition">The visibility, enablement, propagation, or registration change.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ViewUnavailability_reportsMouseAndTouchExitsOnce(int transition)
    {
        var eventHub = new EventHub();
        var inputSystem = DispatchProxy.Create<IInputSystem, TestInputSystem>();
        var inputProxy = (TestInputSystem)(object)inputSystem;
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var parent = new Element();
        var view = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        parent.Children.Add(view);
        parent.Initialize();
        parent.Activate();
        view.Activate();
        view.Arrange(new Rectangle<float>(0f, 0f, 200f, 100f));
        gui.Handle(new GameObjectActivatedEvent(parent));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        inputProxy.RaiseMouseMoved(new(null!, new(100f, 50f)));
        inputProxy.RaiseTouchStarted(new(null!, 3, new(100f, 50f)));
        eventHub.Drain();

        switch (transition)
        {
            case 0: view.IsVisible = false; break;
            case 1: view.IsEnabled = false; break;
            case 2: view.PropagateInputEvents = false; break;
            case 3: gui.Handle(new GameObjectDeactivatedEvent(view)); break;
            case 4: parent.IsVisible = false; break;
            case 5: parent.IsEnabled = false; break;
        }
        gui.Update(0);
        eventHub.Drain();
        Assert.Single(collector.MouseExited);
        Assert.Equal(3, Assert.Single(collector.TouchExited).TouchId);
        Assert.Empty(collector.MouseCanceled);
        Assert.Equal(transition is 2 or 3 ? 1 : 0, collector.TouchCanceled.Count);
        Assert.Empty(collector.TouchEvents.OfType<ViewTouchEndedEvent>());

        gui.Update(0);
        eventHub.Drain();
        Assert.Single(collector.MouseExited);
        Assert.Single(collector.TouchExited);
    }

    /// <summary>Verifies only originating touches receive exits and retain movement and end delivery.</summary>
    [Fact]
    public void TouchTransitions_trackOwnedFingersWithoutAdoptingTouchesThatStartedOutside()
    {
        var eventHub = new EventHub();
        using var inputSystem = new InputSystem(eventHub);
        inputSystem.Initialize();
        var screen = DispatchProxy.Create<ITouchScreenInputDevice, TestInputSystem>();
        var otherScreen = DispatchProxy.Create<ITouchScreenInputDevice, TestInputSystem>();
        var gui = new GraphicalUserInterface(
            eventHub, new TestGraphicsSystem(), inputSystem: inputSystem
        );
        var view = new View
        {
            Camera = new OrthoCamera(),
            PropagateInputEvents = true,
            SizingMode = ViewSizingMode.Stretch,
        };
        view.Initialize();
        view.Activate();
        view.Arrange(new Rectangle<float>(100f, 50f, 200f, 100f));
        gui.Handle(new GameObjectActivatedEvent(view));
        var collector = new ViewInputEventCollector();
        eventHub.Register(collector);
        var inside = new Vector2D<float>(200f, 100f);
        var outside = new Vector2D<float>(400f, 100f);

        inputSystem.ReportTouchStarted(new(screen, 1, outside));
        inputSystem.ReportTouchMoved(new(screen, 1, inside));
        inputSystem.ReportTouchEnded(new(screen, 1, inside));
        eventHub.Drain();
        Assert.Empty(collector.TouchEvents);
        Assert.Empty(collector.TouchExited);

        inputSystem.ReportTouchStarted(new(screen, 2, inside));
        inputSystem.ReportTouchStarted(new(otherScreen, 2, inside));
        inputSystem.ReportTouchMoved(new(screen, 2, outside));
        inputSystem.ReportTouchMoved(new(screen, 2, outside));
        eventHub.Drain();
        var exit = Assert.Single(collector.TouchExited);
        Assert.Same(screen, exit.TouchScreen);
        Assert.Equal(2, exit.TouchId);
        Assert.Equal(outside, exit.Position);
        Assert.Equal(2, collector.TouchEvents.OfType<ViewTouchMovedEvent>().Count());
        Assert.Empty(collector.TouchEvents.OfType<ViewTouchEndedEvent>());

        inputSystem.ReportTouchMoved(new(screen, 2, inside));
        inputSystem.ReportTouchMoved(new(screen, 2, outside));
        inputSystem.ReportTouchEnded(new(screen, 2, outside));
        inputSystem.ReportTouchEnded(new(otherScreen, 2, inside));
        inputSystem.ReportTouchMoved(new(screen, 2, inside));
        eventHub.Drain();
        Assert.Equal(2, collector.TouchExited.Count);
        Assert.Equal(4, collector.TouchEvents.OfType<ViewTouchMovedEvent>().Count());
        Assert.Equal(2, collector.TouchEvents.OfType<ViewTouchEndedEvent>().Count());
    }

    /// <summary>Verifies ancestor visibility suppresses and restores the arranged viewport.</summary>
    [Fact]
    public void AncestorVisibility_controlsViewportRendering()
    {
        var parent = new Element();
        var view = new View();
        parent.Children.Add(view);
        var bounds = new Rectangle<float>(10f, 20f, 100f, 80f);
        view.Arrange(bounds);

        parent.IsVisible = false;
        Assert.Equal(new Rectangle<int>(0, 0, 0, 0), view.ViewComponent.ClippingRegion);

        parent.IsVisible = true;
        Assert.Equal(new Rectangle<int>(10, 20, 100, 80), view.ViewComponent.ClippingRegion);
    }

    /// <summary>Intercepts direct pointer events for view input tests.</summary>
    private class TestInputSystem : DispatchProxy
    {
        private Action<MouseMovedEvent>? _mouseMoved;
        private Action<MouseButtonPressedEvent>? _mouseButtonPressed;
        private Action<MouseButtonReleasedEvent>? _mouseButtonReleased;
        private Action<MouseCanceledEvent>? _mouseCanceled;
        private Action<MouseDisconnectedEvent>? _mouseDisconnected;
        private Action<TouchStartedEvent>? _touchStarted;
        private Action<TouchMovedEvent>? _touchMoved;
        private Action<TouchEndedEvent>? _touchEnded;

        /// <summary>Raises a mouse-movement event through the proxied input-system interface.</summary>
        /// <param name="message">The event to raise.</param>
        public void RaiseMouseMoved(MouseMovedEvent message) => _mouseMoved?.Invoke(message);

        /// <summary>Raises a direct mouse-button press.</summary>
        /// <param name="message">The event to raise.</param>
        public void RaiseMouseButtonPressed(MouseButtonPressedEvent message) =>
            _mouseButtonPressed?.Invoke(message);

        /// <summary>Raises a direct mouse cancellation.</summary>
        /// <param name="message">The event to raise.</param>
        public void RaiseMouseCanceled(MouseCanceledEvent message) => _mouseCanceled?.Invoke(message);

        /// <summary>Raises an immediate mouse disconnection.</summary>
        /// <param name="message">The device disconnection to raise.</param>
        public void RaiseMouseDisconnected(MouseDisconnectedEvent message) => _mouseDisconnected?.Invoke(message);

        /// <summary>Raises a direct mouse-button release through the input-system interface.</summary>
        /// <param name="message">The event to raise.</param>
        public void RaiseMouseButtonReleased(MouseButtonReleasedEvent message) =>
            _mouseButtonReleased?.Invoke(message);

        /// <summary>Raises a direct touch-start event through the input-system interface.</summary>
        /// <param name="message">The event to raise.</param>
        public void RaiseTouchStarted(TouchStartedEvent message) => _touchStarted?.Invoke(message);

        /// <summary>Raises an immediate touch movement.</summary>
        /// <param name="message">The touch movement to raise.</param>
        public void RaiseTouchMoved(TouchMovedEvent message) => _touchMoved?.Invoke(message);

        /// <summary>Raises a direct touch-end event.</summary>
        /// <param name="message">The event to raise.</param>
        public void RaiseTouchEnded(TouchEndedEvent message) => _touchEnded?.Invoke(message);

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "add_MouseMoved")
                _mouseMoved += (Action<MouseMovedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_MouseMoved")
                _mouseMoved -= (Action<MouseMovedEvent>)args![0]!;
            else if (targetMethod?.Name == "add_MouseButtonPressed")
                _mouseButtonPressed += (Action<MouseButtonPressedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_MouseButtonPressed")
                _mouseButtonPressed -= (Action<MouseButtonPressedEvent>)args![0]!;
            else if (targetMethod?.Name == "add_MouseButtonReleased")
                _mouseButtonReleased += (Action<MouseButtonReleasedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_MouseButtonReleased")
                _mouseButtonReleased -= (Action<MouseButtonReleasedEvent>)args![0]!;
            else if (targetMethod?.Name == "add_MouseCanceled")
                _mouseCanceled += (Action<MouseCanceledEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_MouseCanceled")
                _mouseCanceled -= (Action<MouseCanceledEvent>)args![0]!;
            else if (targetMethod?.Name == "add_MouseDisconnected")
                _mouseDisconnected += (Action<MouseDisconnectedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_MouseDisconnected")
                _mouseDisconnected -= (Action<MouseDisconnectedEvent>)args![0]!;
            else if (targetMethod?.Name == "add_TouchStarted")
                _touchStarted += (Action<TouchStartedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_TouchStarted")
                _touchStarted -= (Action<TouchStartedEvent>)args![0]!;
            else if (targetMethod?.Name == "add_TouchMoved")
                _touchMoved += (Action<TouchMovedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_TouchMoved")
                _touchMoved -= (Action<TouchMovedEvent>)args![0]!;
            else if (targetMethod?.Name == "add_TouchEnded")
                _touchEnded += (Action<TouchEndedEvent>)args![0]!;
            else if (targetMethod?.Name == "remove_TouchEnded")
                _touchEnded -= (Action<TouchEndedEvent>)args![0]!;

            return targetMethod?.ReturnType == typeof(void) ? null
                : targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    /// <summary>Collects ray-converted input events from the event hub.</summary>
    private sealed class ViewInputEventCollector
    {
        /// <summary>Gets the mouse-movement events received.</summary>
        public List<ViewMouseMovedEvent> MouseMoved { get; } = [];

        /// <summary>Gets mouse events in their delivery order.</summary>
        public List<IEvent> MouseEvents { get; } = [];

        /// <summary>Gets the per-mouse region-entry events.</summary>
        public List<ViewMouseEnteredEvent> MouseEntered { get; } = [];

        /// <summary>Gets the per-mouse region-exit events.</summary>
        public List<ViewMouseExitedEvent> MouseExited { get; } = [];

        /// <summary>Gets the per-touch region-exit events.</summary>
        public List<ViewTouchExitedEvent> TouchExited { get; } = [];

        /// <summary>Gets the mouse-button press events received.</summary>
        public List<ViewMouseButtonPressedEvent> MousePressed { get; } = [];

        /// <summary>Gets the mouse-cancellation events received.</summary>
        public List<ViewMouseCanceledEvent> MouseCanceled { get; } = [];

        /// <summary>Gets the touch-cancellation events received.</summary>
        public List<ViewTouchCanceledEvent> TouchCanceled { get; } = [];

        /// <summary>Gets the mouse-button release events received.</summary>
        public List<ViewMouseButtonReleasedEvent> MouseReleased { get; } = [];

        /// <summary>Gets the touch-start events received.</summary>
        public List<ViewTouchStartedEvent> TouchStarted { get; } = [];

        /// <summary>Gets the touch start, movement, and end events received.</summary>
        public List<IEvent> TouchEvents { get; } = [];

        /// <summary>Collects a mouse-movement event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewMouseMovedEvent message)
        {
            MouseMoved.Add(message);
            MouseEvents.Add(message);
        }

        /// <summary>Collects a mouse entry into the view.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewMouseEnteredEvent message)
        {
            MouseEntered.Add(message);
            MouseEvents.Add(message);
        }

        /// <summary>Collects a mouse exit from the view.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewMouseExitedEvent message)
        {
            MouseExited.Add(message);
            MouseEvents.Add(message);
        }

        /// <summary>Collects an owned touch's exit from the view.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewTouchExitedEvent message) => TouchExited.Add(message);

        /// <summary>Collects a mouse-button press.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewMouseButtonPressedEvent message)
        {
            MousePressed.Add(message);
            MouseEvents.Add(message);
        }

        /// <summary>Collects a mouse cancellation.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewMouseCanceledEvent message) => MouseCanceled.Add(message);

        /// <summary>Collects a mouse-button release event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewMouseButtonReleasedEvent message)
        {
            MouseReleased.Add(message);
            MouseEvents.Add(message);
        }

        /// <summary>Collects a touch-start event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewTouchStartedEvent message)
        {
            TouchStarted.Add(message);
            TouchEvents.Add(message);
        }

        /// <summary>Collects a touch-movement event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewTouchMovedEvent message) => TouchEvents.Add(message);

        /// <summary>Collects a touch-end event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewTouchEndedEvent message) => TouchEvents.Add(message);

        /// <summary>Collects a touch cancellation.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(ViewTouchCanceledEvent message) => TouchCanceled.Add(message);
    }

    /// <summary>Captures the mouse-button callback for a derived-view test.</summary>
    private sealed class MouseButtonCallbackView : View
    {
        /// <summary>Gets the mouse passed to the pressed callback.</summary>
        public IMouseInputDevice? PressedMouse { get; private set; }

        /// <summary>Gets the button passed to the pressed callback.</summary>
        public MouseButtonEnum? PressedButton { get; private set; }

        /// <summary>Gets the position passed to the pressed callback.</summary>
        public Vector2D<float>? PressedPosition { get; private set; }

        /// <summary>Gets the world ray passed to the pressed callback.</summary>
        public Ray3D<float>? PressedWorldRay { get; private set; }

        /// <inheritdoc/>
        protected override void OnMouseButtonPressed(
            IMouseInputDevice mouse,
            MouseButtonEnum button,
            Vector2D<float> position,
            Ray3D<float> worldRay
        )
        {
            PressedMouse = mouse;
            PressedButton = button;
            PressedPosition = position;
            PressedWorldRay = worldRay;
        }
    }
}
