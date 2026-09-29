namespace Nexus.Input;

using Nexus.Input.Events;

/// <summary>
/// Translates global input events into callbacks configured for a scene or GUI element.
/// </summary>
public sealed class InputMap
{
    private IEventHub? _eventHub;
    private readonly Func<Vector2D<float>, bool>? _hitTest;
    private InputDeviceId? _capturedMousePointerId;
    private readonly Dictionary<KeyEnum, List<Action>> _keyPressedBindings = [];
    private readonly Dictionary<KeyEnum, List<Action>> _keyReleasedBindings = [];
    private readonly Dictionary<MouseButtonEnum, List<Action>> _mousePressedBindings = [];
    private readonly Dictionary<MouseButtonEnum, List<Action>> _mouseReleasedBindings = [];
    private readonly List<Action> _mouseWheelBindings = [];
    private readonly Dictionary<string, List<Action>> _controllerSemanticPressedBindings = [];
    private readonly Dictionary<string, List<Action>> _controllerSemanticReleasedBindings = [];
    private readonly Dictionary<
        (InputDeviceId? ControllerId, int ButtonIndex),
        List<Action>
    > _controllerPressedBindings = [];
    private readonly Dictionary<
        (InputDeviceId? ControllerId, int ButtonIndex),
        List<Action>
    > _controllerReleasedBindings = [];
    private readonly Dictionary<
        (InputDeviceId? ControllerId, int AnalogInputIndex),
        List<Action<ControllerAnalogChangedEvent>>
    > _controllerAnalogBindings = [];

    /// <summary>
    /// Initializes an input map with an optional event hub and mouse hit test.
    /// </summary>
    /// <param name="eventHub">The event hub used to publish events raised by bindings.</param>
    /// <param name="hitTest">Determines whether a mouse position is handled by this map.</param>
    public InputMap(IEventHub? eventHub = null, Func<Vector2D<float>, bool>? hitTest = null)
    {
        _eventHub = eventHub;
        _hitTest = hitTest;
    }

    /// <summary>Registers this map with an event hub and associates it with that hub.</summary>
    /// <param name="eventHub">The event hub that dispatches global input events.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventHub"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The map is already associated with another event hub.</exception>
    public void Register(IEventHub eventHub)
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        if (_eventHub is not null && !ReferenceEquals(_eventHub, eventHub))
            throw new InvalidOperationException(
                "The input map is already associated with another event hub."
            );

        _eventHub = eventHub;
        eventHub.Register(this);
    }

    /// <summary>Unregisters this map from an event hub.</summary>
    /// <param name="eventHub">The event hub from which to unregister.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventHub"/> is null.</exception>
    public void Unregister(IEventHub eventHub)
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        eventHub.Unregister(this);
        _capturedMousePointerId = null;
        if (ReferenceEquals(_eventHub, eventHub))
            _eventHub = null;
    }

    /// <summary>Cancels any mouse interaction currently captured by this map.</summary>
    public void CancelPointerCapture() => _capturedMousePointerId = null;

    /// <summary>Gets the event hub used by event-producing bindings.</summary>
    /// <exception cref="InvalidOperationException">The input map has not been registered with an event hub.</exception>
    private IEventHub EventHub =>
        _eventHub
        ?? throw new InvalidOperationException(
            "The input map must be registered with an event hub before it can raise events."
        );

    /// <summary>Checks whether the mouse position belongs to this map's target.</summary>
    /// <param name="position">The position from a mouse input event.</param>
    /// <returns>True when there is no hit test or the hit test accepts the position.</returns>
    private bool ContainsMousePosition(Vector2D<float> position) =>
        _hitTest is null || _hitTest(position);

    /// <summary>
    /// Gets or sets whether this map suppresses scene input events from global input events.
    /// </summary>
    public bool SuppressSceneInputEvents { get; set; }

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
    /// Selects a mouse-button press for binding configuration.
    /// </summary>
    /// <param name="button">The button that activates the binding.</param>
    /// <returns>A builder for adding effects to the button press.</returns>
    public MouseButtonBinding OnMouseButtonPressed(MouseButtonEnum button) =>
        new(this, _mousePressedBindings, button);

    /// <summary>
    /// Selects a mouse-button release for binding configuration.
    /// </summary>
    /// <param name="button">The button that activates the binding.</param>
    /// <returns>A builder for adding effects to the button release.</returns>
    public MouseButtonBinding OnMouseButtonReleased(MouseButtonEnum button) =>
        new(this, _mouseReleasedBindings, button);

    /// <summary>
    /// Selects mouse-wheel movement for binding configuration.
    /// </summary>
    /// <returns>A builder for adding effects to mouse-wheel movement.</returns>
    public MouseWheelBinding OnMouseWheel() => new(this);

    /// <summary>Selects a button press from one specific controller.</summary>
    /// <param name="controllerId">The controller that activates the binding.</param>
    /// <param name="buttonIndex">The controller-local button index.</param>
    /// <returns>A builder for adding effects to the button press.</returns>
    public ControllerButtonBinding OnControllerButtonPressed(
        InputDeviceId controllerId,
        int buttonIndex
    )
    {
        ValidateControllerBinding(controllerId, buttonIndex);
        return new(this, _controllerPressedBindings, controllerId, buttonIndex);
    }

    /// <summary>Selects a button press from any controller.</summary>
    /// <param name="buttonIndex">The controller-local button index.</param>
    /// <returns>A builder for adding effects to the button press.</returns>
    public ControllerButtonBinding OnAnyControllerButtonPressed(int buttonIndex)
    {
        ValidateControlIndex(buttonIndex, nameof(buttonIndex));
        return new(this, _controllerPressedBindings, null, buttonIndex);
    }

    /// <summary>Selects a semantically named button press from any controller.</summary>
    /// <param name="semanticName">The normalized controller button name.</param>
    /// <returns>A builder for adding effects to the button press.</returns>
    public ControllerSemanticButtonBinding OnAnyControllerButtonPressed(string semanticName) =>
        new(this, _controllerSemanticPressedBindings, ValidateSemanticName(semanticName));

    /// <summary>Selects a button release from one specific controller.</summary>
    /// <param name="controllerId">The controller that activates the binding.</param>
    /// <param name="buttonIndex">The controller-local button index.</param>
    /// <returns>A builder for adding effects to the button release.</returns>
    public ControllerButtonBinding OnControllerButtonReleased(
        InputDeviceId controllerId,
        int buttonIndex
    )
    {
        ValidateControllerBinding(controllerId, buttonIndex);
        return new(this, _controllerReleasedBindings, controllerId, buttonIndex);
    }

    /// <summary>Selects a button release from any controller.</summary>
    /// <param name="buttonIndex">The controller-local button index.</param>
    /// <returns>A builder for adding effects to the button release.</returns>
    public ControllerButtonBinding OnAnyControllerButtonReleased(int buttonIndex)
    {
        ValidateControlIndex(buttonIndex, nameof(buttonIndex));
        return new(this, _controllerReleasedBindings, null, buttonIndex);
    }

    /// <summary>Selects a semantically named button release from any controller.</summary>
    /// <param name="semanticName">The normalized controller button name.</param>
    /// <returns>A builder for adding effects to the button release.</returns>
    public ControllerSemanticButtonBinding OnAnyControllerButtonReleased(string semanticName) =>
        new(this, _controllerSemanticReleasedBindings, ValidateSemanticName(semanticName));

    /// <summary>Selects an analog change from one specific controller.</summary>
    /// <param name="controllerId">The controller that activates the binding.</param>
    /// <param name="analogInputIndex">The controller-local analog-input index.</param>
    /// <returns>A builder for adding effects to the analog event.</returns>
    public ControllerAnalogBinding OnControllerAnalogChanged(
        InputDeviceId controllerId,
        int analogInputIndex
    )
    {
        ValidateControllerBinding(controllerId, analogInputIndex);
        return new(this, _controllerAnalogBindings, controllerId, analogInputIndex);
    }

    /// <summary>Selects an analog change from any controller.</summary>
    /// <param name="analogInputIndex">The controller-local analog-input index.</param>
    /// <returns>A builder for adding effects to the analog event.</returns>
    public ControllerAnalogBinding OnAnyControllerAnalogChanged(int analogInputIndex)
    {
        ValidateControlIndex(analogInputIndex, nameof(analogInputIndex));
        return new(this, _controllerAnalogBindings, null, analogInputIndex);
    }

    /// <summary>
    /// Dispatches callbacks configured for the pressed key.
    /// </summary>
    /// <param name="message">The key press event to handle.</param>
    public void Handle(KeyPressedEvent message)
    {
        if (!SuppressSceneInputEvents)
            Dispatch(_keyPressedBindings, message.Key);
    }

    /// <summary>
    /// Dispatches callbacks configured for the released key.
    /// </summary>
    /// <param name="message">The key release event to handle.</param>
    public void Handle(KeyReleasedEvent message)
    {
        if (!SuppressSceneInputEvents)
            Dispatch(_keyReleasedBindings, message.Key);
    }

    /// <summary>
    /// Dispatches callbacks configured for the pressed mouse button.
    /// </summary>
    /// <param name="message">The mouse-button press event to handle.</param>
    public void Handle(MouseButtonPressedEvent message)
    {
        if (SuppressSceneInputEvents)
            return;

        if (_hitTest is null)
        {
            Dispatch(_mousePressedBindings, message.Button);
            return;
        }

        if (
            message.Button != MouseButtonEnum.Left
            || _capturedMousePointerId.HasValue
            || !ContainsMousePosition(message.Position)
            || (
                !_mousePressedBindings.ContainsKey(message.Button)
                && !_mouseReleasedBindings.ContainsKey(message.Button)
            )
        )
            return;

        _capturedMousePointerId = message.Mouse.Id;
        Dispatch(_mousePressedBindings, message.Button);
    }

    /// <summary>
    /// Dispatches callbacks configured for the released mouse button.
    /// </summary>
    /// <param name="message">The mouse-button release event to handle.</param>
    public void Handle(MouseButtonReleasedEvent message)
    {
        if (_hitTest is null)
        {
            if (!SuppressSceneInputEvents && ContainsMousePosition(message.Position))
                Dispatch(_mouseReleasedBindings, message.Button);
            return;
        }

        if (message.Button != MouseButtonEnum.Left || _capturedMousePointerId != message.Mouse.Id)
            return;

        _capturedMousePointerId = null;
        if (!SuppressSceneInputEvents && ContainsMousePosition(message.Position))
            Dispatch(_mouseReleasedBindings, message.Button);
    }

    /// <summary>
    /// Dispatches callbacks configured for mouse-wheel movement.
    /// </summary>
    /// <param name="message">The mouse-wheel event to handle.</param>
    public void Handle(MouseWheelEvent message)
    {
        if (SuppressSceneInputEvents || !ContainsMousePosition(message.Position))
            return;

        foreach (var callback in _mouseWheelBindings.ToArray())
            callback();
    }

    /// <summary>Clears a captured pointer when its mouse disconnects.</summary>
    /// <param name="message">The mouse-disconnection event.</param>
    public void Handle(MouseDisconnectedEvent message)
    {
        if (_capturedMousePointerId == message.Mouse.Id)
            _capturedMousePointerId = null;
    }

    /// <summary>Clears a captured pointer when mouse interaction is canceled.</summary>
    /// <param name="message">The mouse-cancellation event.</param>
    public void Handle(MouseCanceledEvent message)
    {
        if (_capturedMousePointerId == message.Mouse.Id)
            _capturedMousePointerId = null;
    }

    /// <summary>Dispatches effects configured for a pressed controller button.</summary>
    /// <param name="message">The controller button event to handle.</param>
    public void Handle(ControllerButtonPressedEvent message)
    {
        if (SuppressSceneInputEvents)
            return;

        Dispatch(_controllerPressedBindings, (message.Controller.Id, message.ButtonIndex));
        Dispatch(_controllerPressedBindings, ((InputDeviceId?)null, message.ButtonIndex));
        if (message.Button.SemanticName is { } semanticName)
            Dispatch(_controllerSemanticPressedBindings, semanticName);
    }

    /// <summary>Dispatches effects configured for a released controller button.</summary>
    /// <param name="message">The controller button event to handle.</param>
    public void Handle(ControllerButtonReleasedEvent message)
    {
        if (SuppressSceneInputEvents)
            return;

        Dispatch(_controllerReleasedBindings, (message.Controller.Id, message.ButtonIndex));
        Dispatch(_controllerReleasedBindings, ((InputDeviceId?)null, message.ButtonIndex));
        if (message.Button.SemanticName is { } semanticName)
            Dispatch(_controllerSemanticReleasedBindings, semanticName);
    }

    /// <summary>Dispatches effects configured for a changed controller analog input.</summary>
    /// <param name="message">The captured analog-change event to handle.</param>
    public void Handle(ControllerAnalogChangedEvent message)
    {
        if (SuppressSceneInputEvents)
            return;

        DispatchAnalog(
            _controllerAnalogBindings,
            (message.Controller.Id, message.AnalogInputIndex),
            message
        );
        DispatchAnalog(
            _controllerAnalogBindings,
            ((InputDeviceId?)null, message.AnalogInputIndex),
            message
        );
    }

    /// <summary>
    /// Adds a callback to the specified key binding.
    /// </summary>
    /// <param name="bindings">The press or release bindings.</param>
    /// <param name="key">The key associated with the callback.</param>
    /// <param name="callback">The callback to append.</param>
    private void AddBinding<TBinding, TCallback>(
        Dictionary<TBinding, List<TCallback>> bindings,
        TBinding binding,
        TCallback callback
    )
        where TBinding : notnull
        where TCallback : Delegate
    {
        if (!bindings.TryGetValue(binding, out var callbacks))
        {
            callbacks = [];
            bindings.Add(binding, callbacks);
        }

        callbacks.Add(callback);
    }

    /// <summary>
    /// Adds a callback to the mouse-wheel binding.
    /// </summary>
    /// <param name="callback">The callback to append.</param>
    private void AddWheelBinding(Action callback) => _mouseWheelBindings.Add(callback);

    /// <summary>Validates a controller-specific binding key.</summary>
    /// <param name="controllerId">The controller identifier.</param>
    /// <param name="controlIndex">The controller-local control index.</param>
    private static void ValidateControllerBinding(InputDeviceId controllerId, int controlIndex)
    {
        if (controllerId == InputDeviceId.Invalid)
            throw new ArgumentOutOfRangeException(nameof(controllerId));
        ValidateControlIndex(controlIndex, nameof(controlIndex));
    }

    /// <summary>Validates a controller-local control index.</summary>
    /// <param name="controlIndex">The index to validate.</param>
    /// <param name="parameterName">The originating parameter name.</param>
    private static void ValidateControlIndex(int controlIndex, string parameterName)
    {
        if (controlIndex < 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    /// <summary>Validates a semantic controller button name.</summary>
    /// <param name="semanticName">The semantic button name.</param>
    /// <returns>The validated semantic button name.</returns>
    private static string ValidateSemanticName(string semanticName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticName);
        return semanticName;
    }

    /// <summary>Invokes a snapshot of event-aware callbacks for one controller analog input.</summary>
    /// <param name="bindings">The configured analog callbacks.</param>
    /// <param name="binding">The controller and analog index key.</param>
    /// <param name="message">The captured event passed to each callback.</param>
    private static void DispatchAnalog(
        Dictionary<
            (InputDeviceId? ControllerId, int AnalogInputIndex),
            List<Action<ControllerAnalogChangedEvent>>
        > bindings,
        (InputDeviceId? ControllerId, int AnalogInputIndex) binding,
        ControllerAnalogChangedEvent message
    )
    {
        if (!bindings.TryGetValue(binding, out var callbacks))
            return;

        foreach (var callback in callbacks.ToArray())
            callback(message);
    }

    /// <summary>
    /// Invokes a snapshot of callbacks configured for the key.
    /// </summary>
    /// <param name="bindings">The press or release bindings.</param>
    /// <param name="key">The key whose callbacks should run.</param>
    private static void Dispatch<TBinding>(
        Dictionary<TBinding, List<Action>> bindings,
        TBinding binding
    )
        where TBinding : notnull
    {
        if (!bindings.TryGetValue(binding, out var callbacks))
            return;

        foreach (var callback in callbacks.ToArray())
            callback();
    }

    /// <summary>
    /// Configures effects for one key press or release.
    /// </summary>
    public sealed class KeyBinding
    {
        private readonly InputMap _inputMap;
        private readonly Dictionary<KeyEnum, List<Action>> _bindings;
        private readonly KeyEnum _key;

        /// <summary>
        /// Initializes a key binding builder.
        /// </summary>
        /// <param name="inputMap">The owning input map.</param>
        /// <param name="bindings">The press or release bindings.</param>
        /// <param name="key">The key being configured.</param>
        internal KeyBinding(
            InputMap inputMap,
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
        public InputMap Invoke(Action callback)
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
        public InputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(() => _inputMap.EventHub.Publish(new TEvent()));

        /// <summary>
        /// Adds an effect that creates and publishes an event whenever this binding matches.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="factory">Creates the event to publish.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        public InputMap Raise<TEvent>(Func<TEvent> factory)
            where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(factory);

            return Invoke(() =>
            {
                var @event = factory();
                if (@event is not null)
                    _inputMap.EventHub.Publish(@event);
            });
        }

        /// <summary>
        /// Adds an effect that executes a game input action whenever this binding matches.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public InputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            return Invoke(action.Execute);
        }
    }

    /// <summary>
    /// Configures effects for one mouse-button press or release.
    /// </summary>
    public sealed class MouseButtonBinding
    {
        private readonly InputMap _inputMap;
        private readonly Dictionary<MouseButtonEnum, List<Action>> _bindings;
        private readonly MouseButtonEnum _button;

        /// <summary>
        /// Initializes a mouse-button binding builder.
        /// </summary>
        /// <param name="inputMap">The owning input map.</param>
        /// <param name="bindings">The press or release bindings.</param>
        /// <param name="button">The button being configured.</param>
        internal MouseButtonBinding(
            InputMap inputMap,
            Dictionary<MouseButtonEnum, List<Action>> bindings,
            MouseButtonEnum button
        )
        {
            _inputMap = inputMap;
            _bindings = bindings;
            _button = button;
        }

        /// <summary>
        /// Adds a callback to invoke when this mouse-button binding matches.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        public InputMap Invoke(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _inputMap.AddBinding(_bindings, _button, callback);
            return _inputMap;
        }

        /// <summary>
        /// Adds an effect that publishes a new event instance whenever this binding matches.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(() => _inputMap.EventHub.Publish(new TEvent()));

        /// <summary>
        /// Adds an effect that creates and publishes an event whenever this binding matches.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="factory">Creates the event to publish.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        public InputMap Raise<TEvent>(Func<TEvent> factory)
            where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(factory);

            return Invoke(() =>
            {
                var @event = factory();
                if (@event is not null)
                    _inputMap.EventHub.Publish(@event);
            });
        }

        /// <summary>
        /// Adds an effect that executes a game input action whenever this binding matches.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public InputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Invoke(action.Execute);
        }
    }

    /// <summary>
    /// Configures effects for mouse-wheel movement.
    /// </summary>
    public sealed class MouseWheelBinding
    {
        private readonly InputMap _inputMap;

        /// <summary>
        /// Initializes a mouse-wheel binding builder.
        /// </summary>
        /// <param name="inputMap">The owning input map.</param>
        internal MouseWheelBinding(InputMap inputMap)
        {
            _inputMap = inputMap;
        }

        /// <summary>
        /// Adds a callback to invoke when the mouse wheel moves.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        public InputMap Invoke(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _inputMap.AddWheelBinding(callback);
            return _inputMap;
        }

        /// <summary>
        /// Adds an effect that publishes a new event instance whenever the mouse wheel moves.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(() => _inputMap.EventHub.Publish(new TEvent()));

        /// <summary>
        /// Adds an effect that creates and publishes an event whenever the mouse wheel moves.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="factory">Creates the event to publish.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        public InputMap Raise<TEvent>(Func<TEvent> factory)
            where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(factory);

            return Invoke(() =>
            {
                var @event = factory();
                if (@event is not null)
                    _inputMap.EventHub.Publish(@event);
            });
        }

        /// <summary>
        /// Adds an effect that executes a game input action whenever the mouse wheel moves.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        public InputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Invoke(action.Execute);
        }
    }

    /// <summary>Configures effects for a semantically named button press or release.</summary>
    public sealed class ControllerSemanticButtonBinding
    {
        private readonly InputMap _inputMap;
        private readonly Dictionary<string, List<Action>> _bindings;
        private readonly string _semanticName;

        /// <summary>Initializes a semantic controller-button binding builder.</summary>
        /// <param name="inputMap">The owning input map.</param>
        /// <param name="bindings">The press or release binding table.</param>
        /// <param name="semanticName">The normalized controller button name.</param>
        internal ControllerSemanticButtonBinding(
            InputMap inputMap,
            Dictionary<string, List<Action>> bindings,
            string semanticName
        )
        {
            _inputMap = inputMap;
            _bindings = bindings;
            _semanticName = semanticName;
        }

        /// <summary>Adds a callback to invoke when this binding matches.</summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Invoke(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _inputMap.AddBinding(_bindings, _semanticName, callback);
            return _inputMap;
        }

        /// <summary>Adds an effect that publishes an event when this binding matches.</summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(() => _inputMap.EventHub.Publish(new TEvent()));

        /// <summary>Adds an effect that executes an input action when this binding matches.</summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Invoke(action.Execute);
        }
    }

    /// <summary>Configures effects for one controller button press or release.</summary>
    public sealed class ControllerButtonBinding
    {
        private readonly InputMap _inputMap;
        private readonly Dictionary<
            (InputDeviceId? ControllerId, int ButtonIndex),
            List<Action>
        > _bindings;
        private readonly InputDeviceId? _controllerId;
        private readonly int _buttonIndex;

        /// <summary>Initializes a controller-button binding builder.</summary>
        /// <param name="inputMap">The owning input map.</param>
        /// <param name="bindings">The press or release binding table.</param>
        /// <param name="controllerId">The selected controller, or null for any controller.</param>
        /// <param name="buttonIndex">The selected controller-local button index.</param>
        internal ControllerButtonBinding(
            InputMap inputMap,
            Dictionary<(InputDeviceId? ControllerId, int ButtonIndex), List<Action>> bindings,
            InputDeviceId? controllerId,
            int buttonIndex
        )
        {
            _inputMap = inputMap;
            _bindings = bindings;
            _controllerId = controllerId;
            _buttonIndex = buttonIndex;
        }

        /// <summary>Adds a callback to invoke when this binding matches.</summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Invoke(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _inputMap.AddBinding(_bindings, (_controllerId, _buttonIndex), callback);
            return _inputMap;
        }

        /// <summary>Adds an effect that publishes a new event instance when this binding matches.</summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(() => _inputMap.EventHub.Publish(new TEvent()));

        /// <summary>Adds an effect that creates and publishes an event when this binding matches.</summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="factory">Creates the event to publish.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>(Func<TEvent> factory)
            where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(factory);
            return Invoke(() =>
            {
                var @event = factory();
                if (@event is not null)
                    _inputMap.EventHub.Publish(@event);
            });
        }

        /// <summary>Adds an effect that executes a game input action when this binding matches.</summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Invoke(action.Execute);
        }
    }

    /// <summary>Configures effects for one controller analog input change.</summary>
    public sealed class ControllerAnalogBinding
    {
        private readonly InputMap _inputMap;
        private readonly Dictionary<
            (InputDeviceId? ControllerId, int AnalogInputIndex),
            List<Action<ControllerAnalogChangedEvent>>
        > _bindings;
        private readonly InputDeviceId? _controllerId;
        private readonly int _analogInputIndex;

        /// <summary>Initializes an analog-input binding builder.</summary>
        /// <param name="inputMap">The owning input map.</param>
        /// <param name="bindings">The analog binding table.</param>
        /// <param name="controllerId">The selected controller, or null for any controller.</param>
        /// <param name="analogInputIndex">The selected controller-local analog index.</param>
        internal ControllerAnalogBinding(
            InputMap inputMap,
            Dictionary<
                (InputDeviceId? ControllerId, int AnalogInputIndex),
                List<Action<ControllerAnalogChangedEvent>>
            > bindings,
            InputDeviceId? controllerId,
            int analogInputIndex
        )
        {
            _inputMap = inputMap;
            _bindings = bindings;
            _controllerId = controllerId;
            _analogInputIndex = analogInputIndex;
        }

        /// <summary>Adds a callback that receives the captured analog-change event.</summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Invoke(Action<ControllerAnalogChangedEvent> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _inputMap.AddBinding(_bindings, (_controllerId, _analogInputIndex), callback);
            return _inputMap;
        }

        /// <summary>Adds an effect that publishes a new event instance when this binding matches.</summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>()
            where TEvent : IEvent, new() => Invoke(_ => _inputMap.EventHub.Publish(new TEvent()));

        /// <summary>Adds an effect that creates and publishes an event when this binding matches.</summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="factory">Creates the event to publish.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Raise<TEvent>(Func<TEvent> factory)
            where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(factory);
            return Invoke(_ =>
            {
                var @event = factory();
                if (@event is not null)
                    _inputMap.EventHub.Publish(@event);
            });
        }

        /// <summary>Adds an effect that executes a game input action when this binding matches.</summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The owning input map.</returns>
        public InputMap Execute(IGameInputAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Invoke(_ => action.Execute());
        }
    }
}
