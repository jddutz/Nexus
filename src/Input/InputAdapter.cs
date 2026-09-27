using System.Collections.ObjectModel;

namespace Nexus.Input;

/// <summary>
/// Adapts a Silk.NET input context to the engine input-device contract.
/// </summary>
public sealed class InputAdapter : IInputAdapter, IDisposable
{
    private readonly IInputContext _inputContext;
    private readonly Func<Silk.NET.Input.IInputDevice, Devices.DeviceProfile?>? _profileResolver;
    private readonly Dictionary<int, Devices.Keyboard> _keyboardsByIndex = [];
    private readonly Dictionary<int, Devices.Mouse> _miceByIndex = [];
    private readonly Dictionary<int, Silk.NET.Input.IGamepad> _gamepadsByIndex = [];
    private readonly Dictionary<
        Silk.NET.Input.IInputDevice,
        Devices.Controller
    > _controllersBySource = new(ReferenceEqualityComparer.Instance);
    private readonly List<IKeyboardInputDevice> _keyboards = [];
    private readonly List<IMouseInputDevice> _mice = [];
    private readonly List<IController> _controllers = [];
    private readonly ReadOnlyCollection<IKeyboardInputDevice> _keyboardView;
    private readonly ReadOnlyCollection<IMouseInputDevice> _mouseView;
    private readonly ReadOnlyCollection<IController> _controllerView;
    private bool _disposed;

    /// <summary>
    /// Gets the keyboards currently exposed by this adapter.
    /// </summary>
    public IReadOnlyCollection<IKeyboardInputDevice> Keyboards => _keyboardView;

    /// <summary>
    /// Gets the mice currently exposed by this adapter.
    /// </summary>
    public IReadOnlyCollection<IMouseInputDevice> Mice => _mouseView;

    /// <summary>Gets the controllers currently exposed by this adapter.</summary>
    public IReadOnlyCollection<IController> Controllers => _controllerView;

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

    /// <summary>
    /// Initializes an adapter for the specified Silk.NET input context.
    /// </summary>
    /// <param name="inputContext">The input context to observe.</param>
    /// <param name="profileResolver">Optionally resolves an override profile for each Silk source. Gamepad axis indices reserve two slots per thumbstick index, followed by trigger indices.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inputContext"/> is <see langword="null"/>.</exception>
    public InputAdapter(
        IInputContext inputContext,
        Func<Silk.NET.Input.IInputDevice, Devices.DeviceProfile?>? profileResolver = null
    )
    {
        ArgumentNullException.ThrowIfNull(inputContext);

        _inputContext = inputContext;
        _profileResolver = profileResolver;
        _keyboardView = _keyboards.AsReadOnly();
        _mouseView = _mice.AsReadOnly();
        _controllerView = _controllers.AsReadOnly();
        _inputContext.ConnectionChanged += OnConnectionChanged;

        foreach (var keyboard in _inputContext.Keyboards)
            AddKeyboard(keyboard, notify: false);

        foreach (var mouse in _inputContext.Mice)
            AddMouse(mouse, notify: false);

        foreach (var gamepad in _inputContext.Gamepads)
            AddGamepad(gamepad, notify: false);

        foreach (var joystick in _inputContext.Joysticks)
            AddJoystick(joystick, notify: false);
    }

    /// <summary>
    /// Releases the input-context subscription and disconnects all exposed devices.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _inputContext.ConnectionChanged -= OnConnectionChanged;

        foreach (var index in _keyboardsByIndex.Keys.ToArray())
            RemoveKeyboard(index, notify: true);

        foreach (var index in _miceByIndex.Keys.ToArray())
            RemoveMouse(index, notify: true);

        foreach (var source in _controllersBySource.Keys.ToArray())
            RemoveController(source, notify: true);
    }

    /// <summary>Samples every currently connected controller.</summary>
    /// <param name="deltaTime">Elapsed time in seconds since the previous input update.</param>
    public void Update(double deltaTime)
    {
        if (_disposed)
            return;

        foreach (var controller in _controllers.ToArray())
        {
            if (controller.IsConnected)
                ((Devices.Controller)controller).Update();
        }
    }

    /// <summary>
    /// Updates the exposed keyboard collection when a device connection changes.
    /// </summary>
    /// <param name="device">The device whose connection state changed.</param>
    /// <param name="isConnected">Whether the device is connected.</param>
    private void OnConnectionChanged(Silk.NET.Input.IInputDevice device, bool isConnected)
    {
        if (_disposed)
            return;

        if (device is Silk.NET.Input.IKeyboard keyboard)
        {
            if (isConnected)
                AddKeyboard(keyboard, notify: true);
            else
                RemoveKeyboard(keyboard.Index, notify: true);
        }
        else if (device is Silk.NET.Input.IMouse mouse)
        {
            if (isConnected)
                AddMouse(mouse, notify: true);
            else
                RemoveMouse(mouse.Index, notify: true);
        }
        else if (device is Silk.NET.Input.IGamepad gamepad)
        {
            if (isConnected)
                AddGamepad(gamepad, notify: true);
            else
                RemoveController(gamepad, notify: true);
        }
        else if (device is Silk.NET.Input.IJoystick joystick)
        {
            if (isConnected)
                AddJoystick(joystick, notify: true);
            else
                RemoveController(joystick, notify: true);
        }
    }

    /// <summary>
    /// Adds a keyboard to the exposed collection if it is not already registered.
    /// </summary>
    /// <param name="keyboard">The Silk.NET keyboard to add.</param>
    /// <param name="notify">Whether to raise the connection event.</param>
    private void AddKeyboard(Silk.NET.Input.IKeyboard keyboard, bool notify)
    {
        if (keyboard.Index < 0 || _keyboardsByIndex.ContainsKey(keyboard.Index))
            return;

        var adaptedKeyboard = new Devices.Keyboard(keyboard);
        _keyboardsByIndex.Add(keyboard.Index, adaptedKeyboard);
        _keyboards.Add(adaptedKeyboard);

        if (notify)
            KeyboardConnected?.Invoke(adaptedKeyboard);
    }

    /// <summary>
    /// Removes a keyboard from the exposed collection if it is registered.
    /// </summary>
    /// <param name="index">The Silk.NET device index of the keyboard to remove.</param>
    /// <param name="notify">Whether to raise the disconnection event.</param>
    private void RemoveKeyboard(int index, bool notify)
    {
        if (!_keyboardsByIndex.Remove(index, out var keyboard))
            return;

        _keyboards.Remove(keyboard);
        keyboard.Dispose();

        if (notify)
            KeyboardDisconnected?.Invoke(keyboard);
    }

    /// <summary>
    /// Adds a mouse to the exposed collection if it is not already registered.
    /// </summary>
    /// <param name="mouse">The Silk.NET mouse to add.</param>
    /// <param name="notify">Whether to raise the connection event.</param>
    private void AddMouse(Silk.NET.Input.IMouse mouse, bool notify)
    {
        if (mouse.Index < 0 || _miceByIndex.ContainsKey(mouse.Index))
            return;

        var adaptedMouse = new Devices.Mouse(mouse);
        _miceByIndex.Add(mouse.Index, adaptedMouse);
        _mice.Add(adaptedMouse);

        if (notify)
            MouseConnected?.Invoke(adaptedMouse);
    }

    /// <summary>
    /// Removes a mouse from the exposed collection if it is registered.
    /// </summary>
    /// <param name="index">The Silk.NET device index of the mouse to remove.</param>
    /// <param name="notify">Whether to raise the disconnection event.</param>
    private void RemoveMouse(int index, bool notify)
    {
        if (!_miceByIndex.Remove(index, out var mouse))
            return;

        _mice.Remove(mouse);
        mouse.Dispose();

        if (notify)
            MouseDisconnected?.Invoke(mouse);
    }

    /// <summary>Wraps a gamepad and records its index for recognizing the joystick projection.</summary>
    /// <param name="gamepad">The physical gamepad source.</param>
    /// <param name="notify">Whether to raise the connection event.</param>
    private void AddGamepad(Silk.NET.Input.IGamepad gamepad, bool notify)
    {
        if (gamepad.Index < 0 || _controllersBySource.ContainsKey(gamepad))
            return;

        var buttonSlots =
            gamepad.Buttons.Count == 0 ? 0 : gamepad.Buttons.Max(button => button.Index) + 1;
        var stickSlots =
            gamepad.Thumbsticks.Count == 0 ? 0 : gamepad.Thumbsticks.Max(stick => stick.Index) + 1;
        var triggerSlots =
            gamepad.Triggers.Count == 0 ? 0 : gamepad.Triggers.Max(trigger => trigger.Index) + 1;
        var physicalAxisCount = (stickSlots * 2) + triggerSlots;
        var profile = _profileResolver?.Invoke(gamepad);
        var buttons = profile?.Buttons ?? CreateGamepadButtonMappings(gamepad);
        var analogInputs =
            profile?.AnalogInputs
            ?? CreateGamepadAnalogMappings(
                gamepad,
                stickSlots,
                GetGamepadTriggerNormalizationRule(gamepad)
            );
        profile?.Validate(buttonSlots, physicalAxisCount);

        var controller = new Devices.Controller(
            gamepad.Name,
            buttons,
            analogInputs,
            index => ReadGamepadButton(gamepad, index),
            index => ReadGamepadAxis(gamepad, index, stickSlots),
            () => gamepad.IsConnected
        );
        _controllersBySource.Add(gamepad, controller);
        _gamepadsByIndex[gamepad.Index] = gamepad;
        _controllers.Add(controller);
        if (notify)
            ControllerConnected?.Invoke(controller);
    }

    /// <summary>Wraps a generic joystick unless its active gamepad view already represents it.</summary>
    /// <param name="joystick">The physical joystick source.</param>
    /// <param name="notify">Whether to raise the connection event.</param>
    private void AddJoystick(Silk.NET.Input.IJoystick joystick, bool notify)
    {
        if (joystick.Index < 0 || _controllersBySource.ContainsKey(joystick))
            return;
        if (_gamepadsByIndex.ContainsKey(joystick.Index))
            return;

        var buttonSlots =
            joystick.Buttons.Count == 0 ? 0 : joystick.Buttons.Max(button => button.Index) + 1;
        var axisSlots = joystick.Axes.Count == 0 ? 0 : joystick.Axes.Max(axis => axis.Index) + 1;
        var profile = _profileResolver?.Invoke(joystick);
        var buttons = profile?.Buttons ?? CreateJoystickButtonMappings(joystick);
        var analogInputs = profile?.AnalogInputs ?? CreateJoystickAnalogMappings(joystick);
        profile?.Validate(buttonSlots, axisSlots);
        if (joystick.Hats.Count > 0)
            buttons = AddHatButtonMappings(joystick, buttons, buttonSlots);

        var controller = new Devices.Controller(
            joystick.Name,
            buttons,
            analogInputs,
            index => ReadJoystickButton(joystick, index, buttonSlots),
            index => ReadJoystickAxis(joystick, index),
            () => joystick.IsConnected
        );
        _controllersBySource.Add(joystick, controller);
        _controllers.Add(controller);
        if (notify)
            ControllerConnected?.Invoke(controller);
    }

    /// <summary>Removes a controller only when the exact physical source instance is registered.</summary>
    /// <param name="source">The physical device instance that disconnected.</param>
    /// <param name="notify">Whether to raise the disconnection event.</param>
    private void RemoveController(Silk.NET.Input.IInputDevice source, bool notify)
    {
        if (!_controllersBySource.Remove(source, out var controller))
            return;

        if (
            source is Silk.NET.Input.IGamepad gamepad
            && _gamepadsByIndex.TryGetValue(gamepad.Index, out var registeredGamepad)
            && ReferenceEquals(gamepad, registeredGamepad)
        )
            _gamepadsByIndex.Remove(gamepad.Index);

        _controllers.Remove(controller);
        controller.Dispose();
        if (notify)
            ControllerDisconnected?.Invoke(controller);
    }

    /// <summary>Creates ordered gamepad button mappings using Silk.NET's reported button names.</summary>
    /// <param name="gamepad">The gamepad source.</param>
    /// <returns>Mappings with contiguous Nexus indices.</returns>
    private static ButtonMapping[] CreateGamepadButtonMappings(Silk.NET.Input.IGamepad gamepad) =>
        gamepad
            .Buttons.OrderBy(button => button.Index)
            .Select(
                (button, logicalIndex) =>
                    new ButtonMapping(logicalIndex, button.Index, GetSemanticName(button.Name))
            )
            .ToArray();

    /// <summary>Creates logical stick and trigger mappings from Silk.NET's reported controls.</summary>
    /// <param name="gamepad">The gamepad source.</param>
    /// <param name="stickSlots">The address space reserved for reported thumbstick indices.</param>
    /// <returns>Mappings with contiguous Nexus indices.</returns>
    private static AnalogMapping[] CreateGamepadAnalogMappings(
        Silk.NET.Input.IGamepad gamepad,
        int stickSlots,
        AnalogNormalizationRule triggerNormalization
    )
    {
        var mappings = new List<AnalogMapping>();
        var leftStickIndex = GetThumbstickIndex(gamepad, isLeft: true);
        var rightStickIndex = GetThumbstickIndex(gamepad, isLeft: false);
        var hasStandardTriggerPair =
            gamepad.Triggers.Any(trigger => trigger.Index == 0)
            && gamepad.Triggers.Any(trigger => trigger.Index == 1);
        foreach (var stick in gamepad.Thumbsticks.OrderBy(stick => stick.Index))
        {
            mappings.Add(
                new AnalogMapping(
                    mappings.Count,
                    stick.Index * 2,
                    stick.Index * 2 + 1,
                    stick.Index == leftStickIndex ? Devices.ControllerSemanticNames.LeftStick
                        : stick.Index == rightStickIndex
                            ? Devices.ControllerSemanticNames.RightStick
                        : null
                )
            );
        }

        foreach (var trigger in gamepad.Triggers.OrderBy(trigger => trigger.Index))
        {
            mappings.Add(
                new AnalogMapping(
                    mappings.Count,
                    (stickSlots * 2) + trigger.Index,
                    SemanticName: hasStandardTriggerPair && trigger.Index == 0
                            ? Devices.ControllerSemanticNames.LeftTrigger
                        : hasStandardTriggerPair && trigger.Index == 1
                            ? Devices.ControllerSemanticNames.RightTrigger
                        : null,
                    Normalization: triggerNormalization
                )
            );
        }

        return mappings.ToArray();
    }

    /// <summary>Gets the raw trigger range used by the active Silk.NET gamepad backend.</summary>
    /// <param name="gamepad">The gamepad source.</param>
    /// <returns>The backend's trigger normalization rule.</returns>
    private static AnalogNormalizationRule GetGamepadTriggerNormalizationRule(
        Silk.NET.Input.IGamepad gamepad
    ) =>
        gamepad.GetType().FullName == "Silk.NET.Input.Glfw.GlfwGamepad"
            ? AnalogNormalizationRule.UnipolarMinusOneToOne
            : AnalogNormalizationRule.UnipolarZeroToOne;

    /// <summary>Gets a named Silk.NET thumbstick index when that role is available.</summary>
    /// <param name="gamepad">The gamepad source.</param>
    /// <param name="isLeft">Whether the left thumbstick is requested.</param>
    /// <returns>The reported thumbstick index, or null when unsupported.</returns>
    private static int? GetThumbstickIndex(Silk.NET.Input.IGamepad gamepad, bool isLeft)
    {
        try
        {
            return isLeft ? gamepad.LeftThumbstick().Index : gamepad.RightThumbstick().Index;
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Creates identity mappings for joystick buttons in physical-index order.</summary>
    /// <param name="joystick">The joystick source.</param>
    /// <returns>Mappings preserving every reported physical button.</returns>
    private static ButtonMapping[] CreateJoystickButtonMappings(
        Silk.NET.Input.IJoystick joystick
    ) =>
        joystick
            .Buttons.OrderBy(button => button.Index)
            .Select((button, logicalIndex) => new ButtonMapping(logicalIndex, button.Index))
            .ToArray();

    /// <summary>Creates one identity analog input for each reported joystick axis.</summary>
    /// <param name="joystick">The joystick source.</param>
    /// <returns>Mappings with each single axis placed on X.</returns>
    private static AnalogMapping[] CreateJoystickAnalogMappings(
        Silk.NET.Input.IJoystick joystick
    ) =>
        joystick
            .Axes.OrderBy(axis => axis.Index)
            .Select((axis, logicalIndex) => new AnalogMapping(logicalIndex, axis.Index))
            .ToArray();

    /// <summary>Adds four logical directional buttons for hats not already represented as buttons.</summary>
    /// <param name="joystick">The joystick source.</param>
    /// <param name="buttonMappings">Existing mapped buttons.</param>
    /// <param name="buttonAddressSlots">The physical and synthetic button address space size.</param>
    /// <param name="physicalButtonSlots">The physical button address space size.</param>
    /// <returns>Mappings with hat directions appended without named D-pad duplicates.</returns>
    private static ButtonMapping[] AddHatButtonMappings(
        Silk.NET.Input.IJoystick joystick,
        IReadOnlyList<ButtonMapping> buttonMappings,
        int physicalButtonSlots
    )
    {
        var mappings = buttonMappings.ToList();
        var existingNames = mappings
            .Select(mapping => mapping.SemanticName)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var button in joystick.Buttons)
        {
            var semanticName = GetSemanticName(button.Name);
            if (
                semanticName is not null
                && semanticName.StartsWith("DPad", StringComparison.Ordinal)
            )
                existingNames.Add(semanticName);
        }
        var directions = new (string Name, Silk.NET.Input.Position2D Position)[]
        {
            (Devices.ControllerSemanticNames.DPadUp, Silk.NET.Input.Position2D.Up),
            (Devices.ControllerSemanticNames.DPadRight, Silk.NET.Input.Position2D.Right),
            (Devices.ControllerSemanticNames.DPadDown, Silk.NET.Input.Position2D.Down),
            (Devices.ControllerSemanticNames.DPadLeft, Silk.NET.Input.Position2D.Left),
        };

        foreach (var hat in joystick.Hats.OrderBy(hat => hat.Index))
        foreach (var (name, position) in directions)
        {
            if (existingNames.Contains(name))
                continue;
            var physicalIndex =
                physicalButtonSlots + (hat.Index * 4) + Array.IndexOf(directions, (name, position));
            mappings.Add(new ButtonMapping(mappings.Count, physicalIndex, name));
        }

        return mappings.ToArray();
    }

    /// <summary>Reads a gamepad button by its reported physical index.</summary>
    /// <param name="gamepad">The gamepad source.</param>
    /// <param name="index">The reported physical button index.</param>
    /// <returns>Whether the button is pressed.</returns>
    private static bool ReadGamepadButton(Silk.NET.Input.IGamepad gamepad, int index)
    {
        foreach (var button in gamepad.Buttons)
        {
            if (button.Index == index)
                return button.Pressed;
        }
        return false;
    }

    /// <summary>Reads a virtual gamepad axis backed by a thumbstick component or trigger.</summary>
    /// <param name="gamepad">The gamepad source.</param>
    /// <param name="index">The virtual axis index.</param>
    /// <param name="stickSlots">The address space reserved for thumbstick indices.</param>
    /// <returns>The raw physical value, or zero for an unreported index.</returns>
    private static float ReadGamepadAxis(Silk.NET.Input.IGamepad gamepad, int index, int stickSlots)
    {
        if (index < stickSlots * 2)
        {
            var stickIndex = index / 2;
            foreach (var stick in gamepad.Thumbsticks)
            {
                if (stick.Index == stickIndex)
                    return index % 2 == 0 ? stick.X : stick.Y;
            }
            return 0f;
        }

        var triggerIndex = index - (stickSlots * 2);
        foreach (var trigger in gamepad.Triggers)
        {
            if (trigger.Index == triggerIndex)
                return trigger.Position;
        }
        return 0f;
    }

    /// <summary>Reads a physical joystick button or a synthetic hat-direction address.</summary>
    /// <param name="joystick">The joystick source.</param>
    /// <param name="index">The physical or synthetic address.</param>
    /// <param name="physicalButtonSlots">The number of physical button address slots.</param>
    /// <returns>Whether the requested button is pressed.</returns>
    private static bool ReadJoystickButton(
        Silk.NET.Input.IJoystick joystick,
        int index,
        int physicalButtonSlots
    )
    {
        if (index < physicalButtonSlots)
        {
            foreach (var button in joystick.Buttons)
            {
                if (button.Index == index)
                    return button.Pressed;
            }
            return false;
        }

        var hatAddress = index - physicalButtonSlots;
        var hatIndex = hatAddress / 4;
        var directionIndex = hatAddress % 4;
        foreach (var hat in joystick.Hats)
        {
            if (hat.Index != hatIndex)
                continue;

            var direction = directionIndex switch
            {
                0 => Silk.NET.Input.Position2D.Up,
                1 => Silk.NET.Input.Position2D.Right,
                2 => Silk.NET.Input.Position2D.Down,
                _ => Silk.NET.Input.Position2D.Left,
            };
            return hat.Position.HasFlag(direction);
        }
        return false;
    }

    /// <summary>Reads a joystick axis by its reported physical index.</summary>
    /// <param name="joystick">The joystick source.</param>
    /// <param name="index">The reported physical axis index.</param>
    /// <returns>The raw physical value, or zero when absent.</returns>
    private static float ReadJoystickAxis(Silk.NET.Input.IJoystick joystick, int index)
    {
        foreach (var axis in joystick.Axes)
        {
            if (axis.Index == index)
                return axis.Position;
        }
        return 0f;
    }

    /// <summary>Translates Silk.NET's conventional gamepad button names to Nexus semantic roles.</summary>
    /// <param name="name">The name reported by Silk.NET.</param>
    /// <returns>The normalized role, or <see langword="null"/> for unknown names.</returns>
    private static string? GetSemanticName(ButtonName name) =>
        name switch
        {
            ButtonName.A => Devices.ControllerSemanticNames.FaceBottom,
            ButtonName.B => Devices.ControllerSemanticNames.FaceRight,
            ButtonName.X => Devices.ControllerSemanticNames.FaceLeft,
            ButtonName.Y => Devices.ControllerSemanticNames.FaceTop,
            ButtonName.LeftBumper => Devices.ControllerSemanticNames.LeftBumper,
            ButtonName.RightBumper => Devices.ControllerSemanticNames.RightBumper,
            ButtonName.Back => Devices.ControllerSemanticNames.Back,
            ButtonName.Start => Devices.ControllerSemanticNames.Start,
            ButtonName.Home => Devices.ControllerSemanticNames.Home,
            ButtonName.LeftStick => Devices.ControllerSemanticNames.LeftStickClick,
            ButtonName.RightStick => Devices.ControllerSemanticNames.RightStickClick,
            ButtonName.DPadUp => Devices.ControllerSemanticNames.DPadUp,
            ButtonName.DPadRight => Devices.ControllerSemanticNames.DPadRight,
            ButtonName.DPadDown => Devices.ControllerSemanticNames.DPadDown,
            ButtonName.DPadLeft => Devices.ControllerSemanticNames.DPadLeft,
            _ => null,
        };
}
