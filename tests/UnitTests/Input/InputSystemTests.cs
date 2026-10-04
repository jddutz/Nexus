namespace Tests;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SilkKey = Silk.NET.Input.Key;

/// <summary>
/// Verifies keyboard registration and global input-event publication.
/// </summary>
public class InputSystemTests
{
    /// <summary>
    /// Verifies keyboards present at initialization are registered and announced.
    /// </summary>
    [Fact]
    public void Initialize_registersAndPublishesExistingKeyboards()
    {
        var keyboard = new FakeKeyboard(1);
        var adapter = new FakeInputAdapter(keyboard);
        var eventHub = new EventHub();
        var events = new InputEventCollector();
        eventHub.Register(events);
        using var inputSystem = new InputSystem(eventHub, adapter);

        inputSystem.Initialize();
        eventHub.Drain();

        Assert.Same(keyboard, inputSystem.Keyboard[keyboard.Id]);
        Assert.Collection(events.Connected, item => Assert.Same(keyboard, item.Keyboard));
    }

    /// <summary>Verifies focus loss publishes cancellation instead of a normal mouse release.</summary>
    [Fact]
    public void WindowFocusLoss_publishesMouseCancellationThroughEventHub()
    {
        var eventHub = new EventHub();
        var mouse = new FakeMouse(12);
        mouse.Move(new(12f, 34f));
        var adapter = new FakeInputAdapter();
        adapter.Connect(mouse);
        var window = DispatchProxy.Create<IWindow, TestFocusWindow>();
        var focusWindow = (TestFocusWindow)(object)window;
        var cancellations = new List<MouseCanceledEvent>();
        var collector = new MouseCancellationCollector(cancellations);
        eventHub.Register(collector);
        using var inputSystem = new InputSystem(eventHub, adapter, window);
        inputSystem.Initialize();

        focusWindow.ChangeFocus(false);
        eventHub.Drain();
        focusWindow.ChangeFocus(true);
        eventHub.Drain();

        Assert.Collection(
            cancellations,
            message =>
            {
                Assert.Same(mouse, message.Mouse);
                Assert.Equal(new Vector2D<float>(12f, 34f), message.Position);
            }
        );
    }

    /// <summary>Verifies controller mappings, normalized state, transitions, and disconnection behavior.</summary>
    [Fact]
    public void Controller_exposesMappedStateAndClearsCachedValuesOnDisconnect()
    {
        var pressed = false;
        var connected = true;
        var axisValue = -1f;
        using var controller = new Controller(
            "Test controller",
            [new ButtonMapping(0, 2, "FaceLeft")],
            [
                new AnalogMapping(
                    0,
                    1,
                    SemanticName: "LeftTrigger",
                    Normalization: AnalogNormalizationRule.UnipolarMinusOneToOne
                ),
            ],
            index => index == 2 && pressed,
            index => index == 1 ? axisValue : 0f,
            () => connected
        );
        var pressedButtons = new List<int>();
        var analogPositions = new List<Vector2D<float>>();
        controller.ButtonPressed += (_, button) => pressedButtons.Add(button.Index);
        controller.AnalogChanged += (_, _, position) => analogPositions.Add(position);

        Assert.NotEqual(InputDeviceId.Invalid, controller.Id);
        Assert.False(controller.Button(0).IsPressed);
        Assert.Equal(Vector2D<float>.Zero, controller.AnalogInput(0).Position);
        Assert.Equal("FaceLeft", controller.Button(0).SemanticName);
        Assert.Equal("LeftTrigger", controller.AnalogInput(0).SemanticName);
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Button(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.AnalogInput(-1));

        pressed = true;
        axisValue = 0f;
        controller.Update();
        axisValue = 0.5f;
        controller.Update();

        Assert.True(controller.Button(0).IsPressed);
        Assert.Collection(pressedButtons, index => Assert.Equal(0, index));
        Assert.Collection(
            analogPositions,
            position => Assert.Equal(new Vector2D<float>(0.5f, 0), position),
            position => Assert.Equal(new Vector2D<float>(0.75f, 0), position)
        );

        connected = false;
        controller.Dispose();
        Assert.False(controller.IsConnected);
        Assert.False(controller.Button(0).IsPressed);
        Assert.Equal(Vector2D<float>.Zero, controller.AnalogInput(0).Position);

        var invertedAxis = 1f;
        using var invertedController = new Controller(
            "Inverted trigger",
            [],
            [
                new AnalogMapping(
                    0,
                    0,
                    InvertX: true,
                    Normalization: AnalogNormalizationRule.UnipolarMinusOneToOne
                ),
            ],
            _ => false,
            _ => invertedAxis,
            () => true
        );
        Assert.Equal(Vector2D<float>.Zero, invertedController.AnalogInput(0).Position);
        invertedAxis = -1f;
        invertedController.Update();
        Assert.Equal(new Vector2D<float>(1f, 0f), invertedController.AnalogInput(0).Position);
    }

    /// <summary>Verifies profile validation rejects invalid logical and physical mappings.</summary>
    [Fact]
    public void DeviceProfile_validatesLogicalIndicesPhysicalRangesAndAxisOwnership()
    {
        Assert.Throws<ArgumentException>(() => new DeviceProfile([new ButtonMapping(1, 0)], []));
        Assert.Throws<ArgumentException>(() =>
            new DeviceProfile([], [new AnalogMapping(0, 0), new AnalogMapping(1, 0)])
        );

        var profile = new DeviceProfile([new ButtonMapping(0, 1)], [new AnalogMapping(0, 2, 3)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Validate(1, 3));
    }

    /// <summary>Verifies controller events capture intermediate values and bindings select the intended devices.</summary>
    [Fact]
    public void Controllers_publishCapturedTransitionsAndDispatchSpecificAndAnyBindings()
    {
        var first = CreateTestController("First", out var firstState);
        var second = CreateTestController("Second", out var secondState);
        var adapter = new FakeInputAdapter();
        var eventHub = new EventHub();
        var collector = new InputEventCollector();
        eventHub.Register(collector);
        using var inputSystem = new InputSystem(eventHub, adapter);
        adapter.Connect(first);
        inputSystem.Initialize();
        adapter.Connect(second);
        Assert.True(inputSystem.TryGetController(first.Id, out var found));
        Assert.Same(first, found);
        Assert.Equal(2, inputSystem.Controllers.Count);

        var calls = new List<string>();
        var analogPositions = new List<Vector2D<float>>();
        var map = new InputMap(eventHub);
        map.OnControllerButtonPressed(first.Id, 0).Invoke(() => calls.Add("first"));
        map.OnAnyControllerButtonPressed(0).Invoke(() => calls.Add("any"));
        map.OnControllerButtonReleased(second.Id, 0)
            .Execute(new TestInputAction(() => calls.Add("second release")));
        map.OnAnyControllerAnalogChanged(0)
            .Invoke(message => analogPositions.Add(message.Position));
        map.Register(eventHub);

        firstState.Pressed = true;
        firstState.Axis = 0.25f;
        inputSystem.Update(0.016);
        inputSystem.Update(0.016);
        firstState.Axis = 0.75f;
        inputSystem.Update(0.016);
        secondState.Pressed = true;
        inputSystem.Update(0.016);
        secondState.Pressed = false;
        inputSystem.Update(0.016);
        adapter.Disconnect(first);
        eventHub.Drain();

        Assert.Equal(["first", "any", "any", "second release"], calls);
        Assert.Equal(
            [new Vector2D<float>(0.25f, 0), new Vector2D<float>(0.75f, 0)],
            analogPositions
        );
        Assert.Collection(
            collector.ControllerConnected,
            item => Assert.Same(first, item.Controller),
            item => Assert.Same(second, item.Controller)
        );
        Assert.Collection(
            collector.ControllerPressed,
            item =>
            {
                Assert.Equal(0, item.ButtonIndex);
                Assert.True(item.IsPressed);
            },
            item =>
            {
                Assert.Equal(0, item.ButtonIndex);
                Assert.True(item.IsPressed);
            }
        );
        Assert.Collection(
            collector.ControllerReleased,
            item =>
            {
                Assert.Equal(0, item.ButtonIndex);
                Assert.False(item.IsPressed);
            }
        );
        Assert.Collection(
            collector.ControllerAnalogChanged,
            item => Assert.Equal(new Vector2D<float>(0.25f, 0), item.Position),
            item => Assert.Equal(new Vector2D<float>(0.75f, 0), item.Position)
        );
        Assert.Collection(
            collector.ControllerDisconnected,
            item =>
            {
                Assert.Same(first, item.Controller);
                Assert.False(item.Controller.IsConnected);
            }
        );
        Assert.False(inputSystem.TryGetController(first.Id, out _));
        Assert.True(inputSystem.TryGetController(second.Id, out _));
    }

    /// <summary>Creates a controllable controller state used by input-system tests.</summary>
    /// <param name="name">The controller name.</param>
    /// <param name="state">The mutable physical state.</param>
    /// <returns>A controller wrapper using the test state as its source.</returns>
    private static Controller CreateTestController(string name, out TestControllerState state)
    {
        var controllerState = new TestControllerState();
        state = controllerState;
        return new Controller(
            name,
            [new ButtonMapping(0, 0, "FaceBottom")],
            [new AnalogMapping(0, 0)],
            index => index == 0 && controllerState.Pressed,
            index => index == 0 ? controllerState.Axis : 0f,
            () => controllerState.Connected
        );
    }

    /// <summary>
    /// Verifies live connections and disconnections update aggregate keyboard state.
    /// </summary>
    [Fact]
    public void ConnectionChanges_updateStateAndPublishGlobalEvents()
    {
        var adapter = new FakeInputAdapter();
        var eventHub = new EventHub();
        var events = new InputEventCollector();
        eventHub.Register(events);
        using var inputSystem = new InputSystem(eventHub, adapter);
        inputSystem.Initialize();
        var keyboard = new FakeKeyboard(2);

        adapter.Connect(keyboard);
        Assert.Same(keyboard, inputSystem.Keyboard[keyboard.Id]);
        adapter.Disconnect(keyboard);
        eventHub.Drain();

        Assert.Throws<KeyNotFoundException>(() => inputSystem.Keyboard[keyboard.Id]);
        Assert.Collection(events.Connected, item => Assert.Same(keyboard, item.Keyboard));
        Assert.Collection(events.Disconnected, item => Assert.Same(keyboard, item.Keyboard));
    }

    /// <summary>
    /// Verifies mouse connections, state, and transitions are exposed through the input system.
    /// </summary>
    [Fact]
    public void MouseConnectionsAndTransitions_updateStateAndPublishEvents()
    {
        var adapter = new FakeInputAdapter();
        var eventHub = new EventHub();
        var events = new InputEventCollector();
        eventHub.Register(events);
        using var inputSystem = new InputSystem(eventHub, adapter);
        inputSystem.Initialize();
        var mouse = new FakeMouse(12);

        adapter.Connect(mouse);
        Assert.Same(mouse, inputSystem.Mouse[mouse.Id]);
        mouse.Move(new Vector2D<float>(4, 9));
        mouse.Press(MouseButtonEnum.Left);
        mouse.Move(new Vector2D<float>(7, 11));
        mouse.Wheel(new Vector2D<float>(0, 1));
        Assert.Equal(new Vector2D<float>(7, 11), inputSystem.Mouse.Position);
        Assert.True(inputSystem.Mouse.IsButtonDown(MouseButtonEnum.Left));
        mouse.Release(MouseButtonEnum.Left);
        adapter.Disconnect(mouse);
        eventHub.Drain();

        Assert.Throws<KeyNotFoundException>(() => inputSystem.Mouse[mouse.Id]);
        Assert.Collection(
            events.MouseConnected,
            item =>
            {
                Assert.Same(mouse, item.Mouse);
                Assert.Equal(Vector2D<float>.Zero, item.Position);
            }
        );
        Assert.Collection(
            events.MouseDisconnected,
            item =>
            {
                Assert.Same(mouse, item.Mouse);
                Assert.Equal(new Vector2D<float>(7, 11), item.Position);
            }
        );
        Assert.Collection(
            events.MouseMoved,
            item => Assert.Equal(new Vector2D<float>(4, 9), item.Position),
            item => Assert.Equal(new Vector2D<float>(7, 11), item.Position)
        );
        Assert.Collection(
            events.MouseButtonPressed,
            item =>
            {
                Assert.Equal(MouseButtonEnum.Left, item.Button);
                Assert.Equal(new Vector2D<float>(4, 9), item.Position);
            }
        );
        Assert.Collection(
            events.MouseButtonReleased,
            item =>
            {
                Assert.Equal(MouseButtonEnum.Left, item.Button);
                Assert.Equal(new Vector2D<float>(7, 11), item.Position);
            }
        );
        Assert.Collection(
            events.MouseWheels,
            item =>
            {
                Assert.Equal(new Vector2D<float>(0, 1), item.Delta);
                Assert.Equal(new Vector2D<float>(7, 11), item.Position);
            }
        );
    }

    /// <summary>
    /// Verifies mouse button and wheel bindings invoke configured actions.
    /// </summary>
    [Fact]
    public void InputMap_dispatchesMouseButtonAndWheelBindings()
    {
        var eventHub = new EventHub();
        var mouse = new FakeMouse(13);
        var calls = new List<string>();
        var map = new InputMap(eventHub);
        map.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => calls.Add("pressed"));
        map.OnMouseButtonReleased(MouseButtonEnum.Left)
            .Execute(new TestInputAction(() => calls.Add("released")));
        map.OnMouseWheel().Invoke(() => calls.Add("wheel"));
        map.Register(eventHub);

        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseWheelEvent(mouse, new Vector2D<float>(0, 1), mouse.Position));
        eventHub.Drain();

        Assert.Equal(["pressed", "released", "wheel"], calls);
        map.Unregister(eventHub);
    }

    /// <summary>
    /// Verifies suppressing scene input events skips all bindings until suppression is cleared.
    /// </summary>
    [Fact]
    public void InputMap_suppressesGlobalInputEventProcessing()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(14);
        var mouse = new FakeMouse(15);
        var calls = new List<string>();
        var map = new InputMap(eventHub) { SuppressSceneInputEvents = true };
        map.OnKeyPressed(KeyEnum.A).Invoke(() => calls.Add("key pressed"));
        map.OnKeyReleased(KeyEnum.A).Invoke(() => calls.Add("key released"));
        map.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => calls.Add("button pressed"));
        map.OnMouseButtonReleased(MouseButtonEnum.Left).Invoke(() => calls.Add("button released"));
        map.OnMouseWheel().Invoke(() => calls.Add("wheel"));
        map.Register(eventHub);

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Publish(new KeyReleasedEvent(keyboard, KeyEnum.A));
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseWheelEvent(mouse, new Vector2D<float>(0, 1), mouse.Position));
        eventHub.Drain();

        Assert.Empty(calls);

        map.SuppressSceneInputEvents = false;
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Publish(new KeyReleasedEvent(keyboard, KeyEnum.A));
        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseWheelEvent(mouse, new Vector2D<float>(0, 1), mouse.Position));
        eventHub.Drain();

        Assert.Equal(
            ["key pressed", "key released", "button pressed", "button released", "wheel"],
            calls
        );
        map.Unregister(eventHub);
    }

    /// <summary>
    /// Verifies key transitions are published with their source keyboard and key identifier.
    /// </summary>
    [Fact]
    public void KeyTransitions_publishGlobalEventsForConnectedKeyboard()
    {
        var keyboard = new FakeKeyboard(3);
        var adapter = new FakeInputAdapter(keyboard);
        var eventHub = new EventHub();
        var events = new InputEventCollector();
        eventHub.Register(events);
        using var inputSystem = new InputSystem(eventHub, adapter);
        inputSystem.Initialize();

        keyboard.Press(KeyEnum.A);
        Assert.True(inputSystem.Keyboard.IsKeyDown(KeyEnum.A));
        keyboard.Release(KeyEnum.A);
        Assert.False(inputSystem.Keyboard.IsKeyDown(KeyEnum.A));
        eventHub.Drain();

        Assert.Collection(
            events.Pressed,
            item =>
            {
                Assert.Same(keyboard, item.Keyboard);
                Assert.Equal(KeyEnum.A, item.Key);
            }
        );
        Assert.Collection(
            events.Released,
            item =>
            {
                Assert.Same(keyboard, item.Keyboard);
                Assert.Equal(KeyEnum.A, item.Key);
            }
        );
    }

    /// <summary>
    /// Verifies a key remains down while any connected keyboard reports it down.
    /// </summary>
    [Fact]
    public void KeyboardState_aggregatesKeyStateAcrossDevices()
    {
        var first = new FakeKeyboard(4);
        var second = new FakeKeyboard(5);
        var adapter = new FakeInputAdapter(first, second);
        using var inputSystem = new InputSystem(new EventHub(), adapter);
        inputSystem.Initialize();

        first.Press(KeyEnum.Space);
        second.Press(KeyEnum.Space);
        first.Release(KeyEnum.Space);

        Assert.True(inputSystem.Keyboard.IsKeyDown(KeyEnum.Space));
    }

    /// <summary>
    /// Verifies press and release bindings execute in registration order only during dispatch.
    /// </summary>
    [Fact]
    public void InputMap_dispatchesMatchingBindingsInOrder()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(7);
        var calls = new List<string>();
        var map = new InputMap(eventHub);
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls.Add("first"));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls.Add("second"));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => calls.Add("release"));
        map.Register(eventHub);

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        Assert.Empty(calls);
        eventHub.Drain();

        Assert.Equal(["first", "second"], calls);

        eventHub.Publish(new KeyReleasedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();

        Assert.Equal(["first", "second", "release"], calls);
        map.Unregister(eventHub);
    }

    /// <summary>
    /// Verifies event bindings create fresh events and defer their delivery to the next drain.
    /// </summary>
    [Fact]
    public void InputMap_raiseAndFactoryPublishOnFollowingDrain()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(8);
        var collector = new InputMapEventCollector();
        eventHub.Register(collector);
        var factoryCalls = 0;
        var map = new InputMap(eventHub);
        map.OnKeyPressed(KeyEnum.Escape).Raise<TestInputEvent>();
        map.OnKeyPressed(KeyEnum.A).Raise(() => new FactoryInputEvent(++factoryCalls));
        map.Register(eventHub);

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Drain();

        Assert.Equal(1, factoryCalls);
        Assert.Empty(collector.CreatedEvents);
        Assert.Empty(collector.FactoryEvents);

        eventHub.Drain();

        Assert.Single(collector.CreatedEvents);
        Assert.Collection(collector.FactoryEvents, message => Assert.Equal(1, message.Value));
        map.Unregister(eventHub);
    }

    /// <summary>
    /// Verifies actions execute once and a scene switch affects subsequent queued events.
    /// </summary>
    [Fact]
    public void InputMap_executesActionsAndSwitchesMapsDuringDrain()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(9);
        var calls = new List<string>();
        var firstMap = new InputMap(eventHub);
        var secondMap = new InputMap(eventHub);
        firstMap
            .OnKeyPressed(KeyEnum.Escape)
            .Execute(
                new TestInputAction(() =>
                {
                    calls.Add("action");
                    firstMap.Unregister(eventHub);
                    secondMap.Register(eventHub);
                })
            );
        secondMap.OnKeyPressed(KeyEnum.A).Invoke(() => calls.Add("second map"));
        firstMap.Register(eventHub);

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Drain();

        Assert.Equal(["action", "second map"], calls);
        secondMap.Unregister(eventHub);
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Drain();
        Assert.Equal(["action", "second map"], calls);
    }

    /// <summary>
    /// Verifies EventHub registration is idempotent and unregistering a map stops delivery.
    /// </summary>
    [Fact]
    public void InputMap_registersAndUnregistersWithEventHub()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(10);
        var calls = 0;
        var map = new InputMap();
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls++);
        map.Register(eventHub);
        map.Register(eventHub);

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, calls);

        map.Unregister(eventHub);
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Verifies GameSystem activates, switches, and deactivates the scene input map through EventHub.
    /// </summary>
    [Fact]
    public void GameSystem_activatesSceneInputMapThroughEventHub()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(11);
        const string sceneName = "InputScene";
        var scene = new Scene(NodeId.New())
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
        var firstCalls = 0;
        var secondCalls = 0;
        var thirdCalls = 0;
        var firstMap = new InputMap();
        firstMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => firstCalls++);
        scene.InputMap = firstMap;

        var nextScene = new Scene(NodeId.New())
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
        var nextMap = new InputMap();
        nextMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => thirdCalls++);
        nextScene.InputMap = nextMap;

        var sceneRegistry = new SceneRegistry();
        sceneRegistry.Register(sceneName, () => scene);
        var gameSystem = new SwitchableGameSystem(
            eventHub,
            sceneRegistry,
            Options.Create(new GameSettings { InitialScene = sceneName })
        );

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(0, firstCalls);

        gameSystem.Initialize();
        gameSystem.Update(0);
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, firstCalls);

        var replacementMap = new InputMap();
        replacementMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => secondCalls++);
        scene.InputMap = replacementMap;
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);

        gameSystem.SwitchScene(nextScene);
        Assert.False(scene.IsActivated);
        Assert.Same(nextScene, gameSystem.CurrentScene);
        Assert.False(nextScene.IsActivated);

        gameSystem.Update(0);

        Assert.True(nextScene.IsActivated);

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);
        Assert.Equal(1, thirdCalls);

        gameSystem.SwitchScene(null);
        Assert.False(nextScene.IsActivated);
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, thirdCalls);
    }

    /// <summary>
    /// Verifies disposal removes keyboard state and stops forwarding key events.
    /// </summary>
    [Fact]
    public void Dispose_unregistersKeyboardsAndStopsForwardingEvents()
    {
        var keyboard = new FakeKeyboard(6);
        var eventHub = new EventHub();
        var events = new InputEventCollector();
        eventHub.Register(events);
        var inputSystem = new InputSystem(eventHub, new FakeInputAdapter(keyboard));
        inputSystem.Initialize();

        inputSystem.Dispose();
        keyboard.Press(KeyEnum.Enter);
        eventHub.Drain();

        Assert.Throws<KeyNotFoundException>(() => inputSystem.Keyboard[keyboard.Id]);
        Assert.Empty(events.Pressed);
    }

    /// <summary>
    /// Verifies Silk.NET key values convert to their engine key equivalents.
    /// </summary>
    [Theory]
    [InlineData(SilkKey.A, KeyEnum.A)]
    [InlineData(SilkKey.Keypad0, KeyEnum.Numpad0)]
    [InlineData((SilkKey)999, KeyEnum.Unknown)]
    public void ToKeyEnum_mapsSilkKeys(SilkKey silkKey, KeyEnum expected)
    {
        Assert.Equal(expected, silkKey.ToKeyEnum());
    }

    /// <summary>
    /// Exposes the protected generated scene setter for lifecycle transition tests.
    /// </summary>
    /// <param name="eventHub">The event hub used by the game system.</param>
    /// <param name="sceneRegistry">The registry used to load the initial scene.</param>
    /// <param name="gameSettings">The game settings.</param>
    private sealed class SwitchableGameSystem(
        IEventHub eventHub,
        ISceneRegistry sceneRegistry,
        IOptions<GameSettings> gameSettings
    ) : GameSystem(eventHub, NullLogger<GameSystem>.Instance, sceneRegistry, gameSettings)
    {
        /// <summary>Changes the current scene using the protected generated setter.</summary>
        /// <param name="scene">The scene to activate, or <see langword="null"/>.</param>
        public void SwitchScene(IScene? scene) => CurrentScene = scene;
    }

    /// <summary>
    /// Provides controllable adapter connection callbacks for input-system tests.
    /// </summary>
    private sealed class FakeInputAdapter(params IKeyboardInputDevice[] keyboards) : IInputAdapter
    {
        private readonly List<IKeyboardInputDevice> _keyboards = [.. keyboards];
        private readonly List<IMouseInputDevice> _mice = [];
        private readonly List<IController> _controllers = [];

        /// <inheritdoc />
        public IReadOnlyCollection<IKeyboardInputDevice> Keyboards => _keyboards;

        /// <inheritdoc />
        public IReadOnlyCollection<IMouseInputDevice> Mice => _mice;

        /// <inheritdoc />
        public IReadOnlyCollection<IController> Controllers => _controllers;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice>? KeyboardConnected;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice>? KeyboardDisconnected;

        /// <inheritdoc />
        public event Action<IMouseInputDevice>? MouseConnected;

        /// <inheritdoc />
        public event Action<IMouseInputDevice>? MouseDisconnected;

        /// <inheritdoc />
        public event Action<IController>? ControllerConnected;

        /// <inheritdoc />
        public event Action<IController>? ControllerDisconnected;

        /// <inheritdoc />
        public void Update(double deltaTime)
        {
            foreach (var controller in _controllers.OfType<Controller>().ToArray())
                controller.Update();
        }

        /// <summary>
        /// Adds a keyboard and raises its connection callback.
        /// </summary>
        /// <param name="keyboard">The keyboard to connect.</param>
        public void Connect(IKeyboardInputDevice keyboard)
        {
            _keyboards.Add(keyboard);
            KeyboardConnected?.Invoke(keyboard);
        }

        /// <summary>
        /// Removes a keyboard and raises its disconnection callback.
        /// </summary>
        /// <param name="keyboard">The keyboard to disconnect.</param>
        public void Disconnect(IKeyboardInputDevice keyboard)
        {
            _keyboards.Remove(keyboard);
            KeyboardDisconnected?.Invoke(keyboard);
        }

        /// <summary>
        /// Adds a mouse and raises its connection callback.
        /// </summary>
        /// <param name="mouse">The mouse to connect.</param>
        public void Connect(IMouseInputDevice mouse)
        {
            _mice.Add(mouse);
            MouseConnected?.Invoke(mouse);
        }

        /// <summary>
        /// Removes a mouse and raises its disconnection callback.
        /// </summary>
        /// <param name="mouse">The mouse to disconnect.</param>
        public void Disconnect(IMouseInputDevice mouse)
        {
            _mice.Remove(mouse);
            MouseDisconnected?.Invoke(mouse);
        }

        /// <summary>Adds a controller and raises its connection callback.</summary>
        /// <param name="controller">The controller to connect.</param>
        public void Connect(IController controller)
        {
            _controllers.Add(controller);
            ControllerConnected?.Invoke(controller);
        }

        /// <summary>Removes and disposes a controller before raising its disconnection callback.</summary>
        /// <param name="controller">The controller to disconnect.</param>
        public void Disconnect(IController controller)
        {
            _controllers.Remove(controller);
            if (controller is IDisposable disposable)
                disposable.Dispose();
            ControllerDisconnected?.Invoke(controller);
        }
    }

    /// <summary>Captures focus-change subscriptions for the InputSystem test.</summary>
    private class TestFocusWindow : DispatchProxy
    {
        private Action<bool>? _focusChanged;

        /// <summary>Raises a focus transition.</summary>
        /// <param name="isFocused">Whether the window is focused.</param>
        public void ChangeFocus(bool isFocused) => _focusChanged?.Invoke(isFocused);

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "add_FocusChanged")
                _focusChanged += (Action<bool>)args![0]!;
            else if (targetMethod?.Name == "remove_FocusChanged")
                _focusChanged -= (Action<bool>)args![0]!;

            return targetMethod?.ReturnType == typeof(void) ? null
                : targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    /// <summary>Collects mouse-cancellation events published by the input system.</summary>
    private sealed class MouseCancellationCollector(List<MouseCanceledEvent> events)
    {
        /// <summary>Records one mouse-cancellation event.</summary>
        /// <param name="message">The cancellation event.</param>
        public void Handle(MouseCanceledEvent message) => events.Add(message);
    }

    /// <summary>Holds mutable physical values for a fake controller source.</summary>
    private sealed class TestControllerState
    {
        /// <summary>Gets or sets whether the physical button is pressed.</summary>
        public bool Pressed { get; set; }

        /// <summary>Gets or sets the raw physical axis position.</summary>
        public float Axis { get; set; }

        /// <summary>Gets or sets whether the physical source remains connected.</summary>
        public bool Connected { get; set; } = true;
    }

    /// <summary>
    /// Provides controllable mouse state and callbacks for input-system tests.
    /// </summary>
    private sealed class FakeMouse(ulong id) : IMouseInputDevice
    {
        private readonly HashSet<MouseButtonEnum> _pressedButtons = [];

        /// <inheritdoc />
        public InputDeviceId Id { get; } = new(id);

        /// <inheritdoc />
        public string Name => $"Mouse {id}";

        /// <inheritdoc />
        public bool IsConnected { get; private set; } = true;

        /// <inheritdoc />
        public Vector2D<float> Position { get; private set; }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? Moved;

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonPressed;

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonReleased;

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? WheelMoved;

        /// <inheritdoc />
        public bool IsButtonDown(MouseButtonEnum button) => _pressedButtons.Contains(button);

        /// <summary>
        /// Updates position and raises the movement callback.
        /// </summary>
        /// <param name="position">The new mouse position.</param>
        public void Move(Vector2D<float> position)
        {
            Position = position;
            Moved?.Invoke(this, position);
        }

        /// <summary>
        /// Marks a button as pressed and raises its callback.
        /// </summary>
        /// <param name="button">The button to press.</param>
        public void Press(MouseButtonEnum button)
        {
            _pressedButtons.Add(button);
            ButtonPressed?.Invoke(this, button);
        }

        /// <summary>
        /// Marks a button as released and raises its callback.
        /// </summary>
        /// <param name="button">The button to release.</param>
        public void Release(MouseButtonEnum button)
        {
            _pressedButtons.Remove(button);
            ButtonReleased?.Invoke(this, button);
        }

        /// <summary>
        /// Raises a wheel-movement callback.
        /// </summary>
        /// <param name="delta">The wheel delta.</param>
        public void Wheel(Vector2D<float> delta) => WheelMoved?.Invoke(this, delta);
    }

    /// <summary>
    /// Provides controllable key state and callbacks for input-system tests.
    /// </summary>
    private sealed class FakeKeyboard(ulong id) : IKeyboardInputDevice
    {
        private readonly HashSet<KeyEnum> _pressedKeys = [];

        /// <inheritdoc />
        public InputDeviceId Id { get; } = new(id);

        /// <inheritdoc />
        public string Name => $"Keyboard {id}";

        /// <inheritdoc />
        public bool IsConnected { get; private set; } = true;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice, KeyEnum>? KeyPressed;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice, KeyEnum>? KeyReleased;

        /// <inheritdoc />
        public bool IsKeyDown(KeyEnum key) => _pressedKeys.Contains(key);

        /// <summary>
        /// Marks a key as pressed and raises its callback.
        /// </summary>
        /// <param name="key">The key to press.</param>
        public void Press(KeyEnum key)
        {
            _pressedKeys.Add(key);
            KeyPressed?.Invoke(this, key);
        }

        /// <summary>
        /// Marks a key as released and raises its callback.
        /// </summary>
        /// <param name="key">The key to release.</param>
        public void Release(KeyEnum key)
        {
            _pressedKeys.Remove(key);
            KeyReleased?.Invoke(this, key);
        }
    }

    /// <summary>
    /// Represents a parameterless event used to verify generic input-map event creation.
    /// </summary>
    private sealed class TestInputEvent : IEvent;

    /// <summary>
    /// Represents an event created by a binding factory.
    /// </summary>
    private sealed class FactoryInputEvent(int value) : IEvent
    {
        /// <summary>Gets the value supplied by the binding factory.</summary>
        public int Value { get; } = value;
    }

    /// <summary>
    /// Provides an action callback for input-map execution tests.
    /// </summary>
    private sealed class TestInputAction(Action callback) : IGameInputAction
    {
        /// <inheritdoc />
        public void Execute() => callback();
    }

    /// <summary>
    /// Collects events raised by input-map bindings.
    /// </summary>
    private sealed class InputMapEventCollector
    {
        /// <summary>Gets generic events published by input bindings.</summary>
        public List<TestInputEvent> CreatedEvents { get; } = [];

        /// <summary>Gets factory-created events published by input bindings.</summary>
        public List<FactoryInputEvent> FactoryEvents { get; } = [];

        /// <summary>Collects a generic input-map event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(TestInputEvent message) => CreatedEvents.Add(message);

        /// <summary>Collects a factory-created input-map event.</summary>
        /// <param name="message">The event to collect.</param>
        public void Handle(FactoryInputEvent message) => FactoryEvents.Add(message);
    }

    /// <summary>
    /// Collects input messages dispatched by the global event hub.
    /// </summary>
    private sealed class InputEventCollector
    {
        /// <summary>Gets keyboard connection messages.</summary>
        public List<KeyboardConnectedEvent> Connected { get; } = [];

        /// <summary>Gets keyboard disconnection messages.</summary>
        public List<KeyboardDisconnectedEvent> Disconnected { get; } = [];

        /// <summary>Gets key-pressed messages.</summary>
        public List<KeyPressedEvent> Pressed { get; } = [];

        /// <summary>Gets key-released messages.</summary>
        public List<KeyReleasedEvent> Released { get; } = [];

        /// <summary>Gets mouse connection messages.</summary>
        public List<MouseConnectedEvent> MouseConnected { get; } = [];

        /// <summary>Gets mouse disconnection messages.</summary>
        public List<MouseDisconnectedEvent> MouseDisconnected { get; } = [];

        /// <summary>Gets mouse movement messages.</summary>
        public List<MouseMovedEvent> MouseMoved { get; } = [];

        /// <summary>Gets mouse button press messages.</summary>
        public List<MouseButtonPressedEvent> MouseButtonPressed { get; } = [];

        /// <summary>Gets mouse button release messages.</summary>
        public List<MouseButtonReleasedEvent> MouseButtonReleased { get; } = [];

        /// <summary>Gets mouse-wheel messages.</summary>
        public List<MouseWheelEvent> MouseWheels { get; } = [];

        /// <summary>Gets controller connection messages.</summary>
        public List<ControllerConnectedEvent> ControllerConnected { get; } = [];

        /// <summary>Gets controller disconnection messages.</summary>
        public List<ControllerDisconnectedEvent> ControllerDisconnected { get; } = [];

        /// <summary>Gets controller button press messages.</summary>
        public List<ControllerButtonPressedEvent> ControllerPressed { get; } = [];

        /// <summary>Gets controller button release messages.</summary>
        public List<ControllerButtonReleasedEvent> ControllerReleased { get; } = [];

        /// <summary>Gets controller analog change messages.</summary>
        public List<ControllerAnalogChangedEvent> ControllerAnalogChanged { get; } = [];

        /// <summary>Collects a keyboard connection message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(KeyboardConnectedEvent message) => Connected.Add(message);

        /// <summary>Collects a keyboard disconnection message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(KeyboardDisconnectedEvent message) => Disconnected.Add(message);

        /// <summary>Collects a key-pressed message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(KeyPressedEvent message) => Pressed.Add(message);

        /// <summary>Collects a key-released message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(KeyReleasedEvent message) => Released.Add(message);

        /// <summary>Collects a mouse connection message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(MouseConnectedEvent message) => MouseConnected.Add(message);

        /// <summary>Collects a mouse disconnection message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(MouseDisconnectedEvent message) => MouseDisconnected.Add(message);

        /// <summary>Collects a mouse movement message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(MouseMovedEvent message) => MouseMoved.Add(message);

        /// <summary>Collects a mouse button press message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(MouseButtonPressedEvent message) => MouseButtonPressed.Add(message);

        /// <summary>Collects a mouse button release message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(MouseButtonReleasedEvent message) => MouseButtonReleased.Add(message);

        /// <summary>Collects a mouse-wheel message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(MouseWheelEvent message) => MouseWheels.Add(message);

        /// <summary>Collects a controller connection message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(ControllerConnectedEvent message) => ControllerConnected.Add(message);

        /// <summary>Collects a controller disconnection message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(ControllerDisconnectedEvent message) =>
            ControllerDisconnected.Add(message);

        /// <summary>Collects a controller button press message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(ControllerButtonPressedEvent message) => ControllerPressed.Add(message);

        /// <summary>Collects a controller button release message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(ControllerButtonReleasedEvent message) =>
            ControllerReleased.Add(message);

        /// <summary>Collects a controller analog change message.</summary>
        /// <param name="message">The message to collect.</param>
        public void Handle(ControllerAnalogChangedEvent message) =>
            ControllerAnalogChanged.Add(message);
    }
}
