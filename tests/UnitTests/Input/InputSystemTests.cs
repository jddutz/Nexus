namespace Tests;

using Nexus.Core.Events;
using Nexus.Input;
using Nexus.Input.Devices;
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

        /// <inheritdoc />
        public IReadOnlyCollection<IKeyboardInputDevice> Keyboards => _keyboards;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice>? KeyboardConnected;

        /// <inheritdoc />
        public event Action<IKeyboardInputDevice>? KeyboardDisconnected;

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
    }
}
