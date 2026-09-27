namespace Tests;

using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;
using Silk.NET.Maths;
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
    public void SceneInputMap_dispatchesMouseButtonAndWheelBindings()
    {
        var eventHub = new EventHub();
        var mouse = new FakeMouse(13);
        var inputSystem = new InputSystem(eventHub);
        var calls = new List<string>();
        var map = new SceneInputMap(eventHub);
        map.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => calls.Add("pressed"));
        map.OnMouseButtonReleased(MouseButtonEnum.Left)
            .Execute(new TestInputAction(() => calls.Add("released")));
        map.OnMouseWheel().Invoke(() => calls.Add("wheel"));
        inputSystem.CurrentMap = map;

        eventHub.Publish(new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, mouse.Position));
        eventHub.Publish(new MouseWheelEvent(mouse, new Vector2D<float>(0, 1), mouse.Position));
        eventHub.Drain();

        Assert.Equal(["pressed", "released", "wheel"], calls);
        inputSystem.Dispose();
    }

    /// <summary>
    /// Verifies suppressing scene input events skips all bindings until suppression is cleared.
    /// </summary>
    [Fact]
    public void SceneInputMap_suppressesGlobalInputEventProcessing()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(14);
        var mouse = new FakeMouse(15);
        var inputSystem = new InputSystem(eventHub);
        var calls = new List<string>();
        var map = new SceneInputMap(eventHub) { SuppressSceneInputEvents = true };
        map.OnKeyPressed(KeyEnum.A).Invoke(() => calls.Add("key pressed"));
        map.OnKeyReleased(KeyEnum.A).Invoke(() => calls.Add("key released"));
        map.OnMouseButtonPressed(MouseButtonEnum.Left).Invoke(() => calls.Add("button pressed"));
        map.OnMouseButtonReleased(MouseButtonEnum.Left).Invoke(() => calls.Add("button released"));
        map.OnMouseWheel().Invoke(() => calls.Add("wheel"));
        inputSystem.CurrentMap = map;

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
        inputSystem.Dispose();
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
    public void SceneInputMap_dispatchesMatchingBindingsInOrder()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(7);
        var inputSystem = new InputSystem(eventHub);
        var calls = new List<string>();
        var map = new SceneInputMap(eventHub);
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls.Add("first"));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls.Add("second"));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => calls.Add("release"));
        inputSystem.CurrentMap = map;

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        Assert.Empty(calls);
        eventHub.Drain();

        Assert.Equal(["first", "second"], calls);

        eventHub.Publish(new KeyReleasedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();

        Assert.Equal(["first", "second", "release"], calls);
        inputSystem.Dispose();
    }

    /// <summary>
    /// Verifies event bindings create fresh events and defer their delivery to the next drain.
    /// </summary>
    [Fact]
    public void SceneInputMap_raiseAndFactoryPublishOnFollowingDrain()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(8);
        var collector = new InputMapEventCollector();
        eventHub.Register(collector);
        var inputSystem = new InputSystem(eventHub);
        var factoryCalls = 0;
        var map = new SceneInputMap(eventHub);
        map.OnKeyPressed(KeyEnum.Escape).Raise<TestInputEvent>();
        map.OnKeyPressed(KeyEnum.A).Raise(() => new FactoryInputEvent(++factoryCalls));
        inputSystem.CurrentMap = map;

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Drain();

        Assert.Equal(1, factoryCalls);
        Assert.Empty(collector.CreatedEvents);
        Assert.Empty(collector.FactoryEvents);

        eventHub.Drain();

        Assert.Single(collector.CreatedEvents);
        Assert.Collection(collector.FactoryEvents, message => Assert.Equal(1, message.Value));
        inputSystem.Dispose();
    }

    /// <summary>
    /// Verifies actions execute once and a scene switch affects subsequent queued events.
    /// </summary>
    [Fact]
    public void SceneInputMap_executesActionsAndSwitchesMapsDuringDrain()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(9);
        var inputSystem = new InputSystem(eventHub);
        var calls = new List<string>();
        var firstMap = new SceneInputMap(eventHub);
        var secondMap = new SceneInputMap(eventHub);
        firstMap
            .OnKeyPressed(KeyEnum.Escape)
            .Execute(
                new TestInputAction(() =>
                {
                    calls.Add("action");
                    inputSystem.CurrentMap = secondMap;
                })
            );
        secondMap.OnKeyPressed(KeyEnum.A).Invoke(() => calls.Add("second map"));
        inputSystem.CurrentMap = firstMap;

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Drain();

        Assert.Equal(["action", "second map"], calls);
        inputSystem.CurrentMap = null;
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.A));
        eventHub.Drain();
        Assert.Equal(["action", "second map"], calls);

        inputSystem.Dispose();
    }

    /// <summary>
    /// Verifies selecting the same map twice does not duplicate delivery and disposal unregisters it.
    /// </summary>
    [Fact]
    public void InputSystem_currentMapIsIdempotentAndUnregisteredOnDispose()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(10);
        var inputSystem = new InputSystem(eventHub);
        var calls = 0;
        var map = new SceneInputMap(eventHub);
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls++);
        inputSystem.CurrentMap = map;
        inputSystem.CurrentMap = map;

        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, calls);

        inputSystem.Dispose();
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Verifies scene activation selects its input map and deactivation clears only that selection.
    /// </summary>
    [Fact]
    public void Scene_activationAndDeactivationSelectItsInputMap()
    {
        var eventHub = new EventHub();
        var keyboard = new FakeKeyboard(11);
        using var inputSystem = new InputSystem(eventHub);
        var calls = 0;
        var firstMap = new SceneInputMap(eventHub);
        firstMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => calls++);
        var firstScene = new Scene(inputSystem) { InputMap = firstMap };

        firstScene.Activate();
        Assert.Same(firstMap, inputSystem.CurrentMap);
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, calls);

        var secondMap = new SceneInputMap(eventHub);
        var secondScene = new Scene(inputSystem) { InputMap = secondMap };
        secondScene.Activate();
        firstScene.Deactivate();
        Assert.Same(secondMap, inputSystem.CurrentMap);

        secondScene.Deactivate();
        Assert.Null(inputSystem.CurrentMap);
        eventHub.Publish(new KeyPressedEvent(keyboard, KeyEnum.Escape));
        eventHub.Drain();
        Assert.Equal(1, calls);
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
    /// Provides controllable adapter connection callbacks for input-system tests.
    /// </summary>
    private sealed class FakeInputAdapter(params IKeyboardInputDevice[] keyboards) : IInputAdapter
    {
        private readonly List<IKeyboardInputDevice> _keyboards = [.. keyboards];
        private readonly List<IMouseInputDevice> _mice = [];

        /// <inheritdoc />
        public IReadOnlyCollection<IKeyboardInputDevice> Keyboards => _keyboards;

        /// <inheritdoc />
        public IReadOnlyCollection<IMouseInputDevice> Mice => _mice;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice>? KeyboardConnected;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice>? KeyboardDisconnected;

        /// <inheritdoc />
        public event Action<IMouseInputDevice>? MouseConnected;

        /// <inheritdoc />
        public event Action<IMouseInputDevice>? MouseDisconnected;

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
    }
}
