namespace Nexus.Input;

using Nexus.Input.Events;

/// <summary>
/// Translates keyboard events into callbacks configured for a scene.
/// </summary>
public sealed class SceneInputMap
{
    private readonly IEventHub _eventHub;
    private readonly Dictionary<KeyEnum, List<Action>> _keyPressedBindings = [];
    private readonly Dictionary<KeyEnum, List<Action>> _keyReleasedBindings = [];

    /// <summary>
    /// Initializes a scene input map that publishes raised events through the specified event hub.
    /// </summary>
    /// <param name="eventHub">The event hub used to publish events raised by bindings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventHub"/> is <see langword="null"/>.</exception>
    public SceneInputMap(IEventHub eventHub)
    {
        ArgumentNullException.ThrowIfNull(eventHub);

        _eventHub = eventHub;
    }

    /// <summary>
    /// Selects a key press for binding configuration.
    /// </summary>
    /// <param name="key">The key that activates the binding.</param>
    /// <returns>A builder for adding effects to the key press.</returns>
    public KeyBinding OnKeyPressed(KeyEnum key) => new(this, _keyPressedBindings, key);

    /// <summary>
    /// Selects a key release for binding configuration.
    /// </summary>
    /// <param name="key">The key that activates the binding.</param>
    /// <returns>A builder for adding effects to the key release.</returns>
    public KeyBinding OnKeyReleased(KeyEnum key) => new(this, _keyReleasedBindings, key);

    /// <summary>
    /// Dispatches callbacks configured for the pressed key.
    /// </summary>
    /// <param name="message">The key press event to handle.</param>
    public void Handle(KeyPressedEvent message) => Dispatch(_keyPressedBindings, message.Key);

    /// <summary>
    /// Dispatches callbacks configured for the released key.
    /// </summary>
    /// <param name="message">The key release event to handle.</param>
    public void Handle(KeyReleasedEvent message) => Dispatch(_keyReleasedBindings, message.Key);

    /// <summary>
    /// Adds a callback to the specified key binding.
    /// </summary>
    /// <param name="bindings">The press or release bindings.</param>
    /// <param name="key">The key associated with the callback.</param>
    /// <param name="callback">The callback to append.</param>
    private void AddBinding(
        Dictionary<KeyEnum, List<Action>> bindings,
        KeyEnum key,
        Action callback
    )
    {
        if (!bindings.TryGetValue(key, out var callbacks))
        {
            callbacks = [];
            bindings.Add(key, callbacks);
        }

        callbacks.Add(callback);
    }

    /// <summary>
    /// Invokes a snapshot of callbacks configured for the key.
    /// </summary>
    /// <param name="bindings">The press or release bindings.</param>
    /// <param name="key">The key whose callbacks should run.</param>
    private static void Dispatch(Dictionary<KeyEnum, List<Action>> bindings, KeyEnum key)
    {
        if (!bindings.TryGetValue(key, out var callbacks))
            return;

        foreach (var callback in callbacks.ToArray())
            callback();
    }

    /// <summary>
    /// Configures effects for one key press or release.
    /// </summary>
    public sealed class KeyBinding
    {
        private readonly SceneInputMap _inputMap;
        private readonly Dictionary<KeyEnum, List<Action>> _bindings;
        private readonly KeyEnum _key;

        /// <summary>
        /// Initializes a key binding builder.
        /// </summary>
        /// <param name="inputMap">The owning input map.</param>
        /// <param name="bindings">The press or release bindings.</param>
        /// <param name="key">The key being configured.</param>
        internal KeyBinding(
            SceneInputMap inputMap,
            Dictionary<KeyEnum, List<Action>> bindings,
            KeyEnum key
        )
        {
            _inputMap = inputMap;
            _bindings = bindings;
            _key = key;
        }

        /// <summary>
        /// Adds a callback to invoke when this key binding matches.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        public SceneInputMap Invoke(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            _inputMap.AddBinding(_bindings, _key, callback);
            return _inputMap;
        }

        /// <summary>
        /// Adds an effect that publishes a new event instance whenever this binding matches.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <returns>The owning input map.</returns>
        public SceneInputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(() => _inputMap._eventHub.Publish(new TEvent()));

        /// <summary>
        /// Adds an effect that creates and publishes an event whenever this binding matches.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="factory">Creates the event to publish.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        public SceneInputMap Raise<TEvent>(Func<TEvent> factory)
            where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(factory);

            return Invoke(() =>
            {
                var @event = factory();
                if (@event is not null)
                    _inputMap._eventHub.Publish(@event);
            });
        }

        /// <summary>
        /// Adds an effect that executes a game input action whenever this binding matches.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public SceneInputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            return Invoke(action.Execute);
        }
    }
}
