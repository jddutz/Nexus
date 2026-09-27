namespace Nexus.Input.Devices;

using System.Collections.ObjectModel;

/// <summary>Wraps one physical controller source and samples its mapped controls.</summary>
public sealed class Controller : IControllerInputDevice, IDisposable
{
    private static long _nextId = 4_294_967_295;
    private readonly Func<bool> _sourceConnected;
    private readonly Func<int, bool> _readButton;
    private readonly Func<int, float> _readAxis;
    private readonly AnalogMapping[] _analogMappings;
    private readonly ReadOnlyCollection<IButtonInput> _buttonView;
    private readonly ReadOnlyCollection<IAnalogInput> _analogView;
    private readonly ButtonInput[] _buttons;
    private readonly AnalogInputState[] _analogInputs;
    private bool _disposed;

    /// <summary>Gets the connection-specific Nexus identifier.</summary>
    public InputDeviceId Id { get; }

    /// <summary>Gets the physical device name.</summary>
    public string Name { get; }

    /// <summary>Gets whether this source remains connected.</summary>
    public bool IsConnected => !_disposed && _sourceConnected();

    /// <summary>Gets the buttons in logical index order.</summary>
    public IReadOnlyList<IButtonInput> Buttons => _buttonView;

    /// <summary>Gets analog inputs in logical index order.</summary>
    public IReadOnlyList<IAnalogInput> AnalogInputs => _analogView;

    /// <summary>Creates a controller wrapper for an input adapter's physical source.</summary>
    /// <param name="name">The reported source name.</param>
    /// <param name="buttonMappings">Mappings for logical buttons.</param>
    /// <param name="analogMappings">Mappings for logical analog inputs.</param>
    /// <param name="readButton">Reads a physical button by source index.</param>
    /// <param name="readAxis">Reads a physical axis by source index.</param>
    /// <param name="sourceConnected">Reads the source connection state.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public Controller(
        string name,
        IEnumerable<ButtonMapping> buttonMappings,
        IEnumerable<AnalogMapping> analogMappings,
        Func<int, bool> readButton,
        Func<int, float> readAxis,
        Func<bool> sourceConnected
    )
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(buttonMappings);
        ArgumentNullException.ThrowIfNull(analogMappings);
        ArgumentNullException.ThrowIfNull(readButton);
        ArgumentNullException.ThrowIfNull(readAxis);
        ArgumentNullException.ThrowIfNull(sourceConnected);

        Name = name;
        _sourceConnected = sourceConnected;
        _readButton = readButton;
        _readAxis = readAxis;
        var buttonMap = buttonMappings.OrderBy(mapping => mapping.LogicalIndex).ToArray();
        _analogMappings = analogMappings.OrderBy(mapping => mapping.LogicalIndex).ToArray();
        new DeviceProfile(buttonMap, _analogMappings);
        Id = new InputDeviceId(unchecked((ulong)Interlocked.Increment(ref _nextId)));
        _buttons = buttonMap.Select(mapping => new ButtonInput(this, mapping)).ToArray();
        _analogInputs = _analogMappings
            .Select(mapping => new AnalogInputState(this, mapping))
            .ToArray();
        _buttonView = Array.AsReadOnly<IButtonInput>(_buttons);
        _analogView = Array.AsReadOnly<IAnalogInput>(_analogInputs);
    }

    /// <summary>Gets the button at the specified logical index.</summary>
    /// <param name="index">The button's controller-local index.</param>
    /// <returns>The requested button.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the button collection.</exception>
    public IButtonInput Button(int index) =>
        (uint)index < (uint)_buttons.Length
            ? _buttons[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Gets the analog input at the specified logical index.</summary>
    /// <param name="index">The analog input's controller-local index.</param>
    /// <returns>The requested analog input.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the analog collection.</exception>
    public IAnalogInput AnalogInput(int index) =>
        (uint)index < (uint)_analogInputs.Length
            ? _analogInputs[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Occurs when a mapped button transitions to pressed.</summary>
    public event Action<IController, IButtonInput>? ButtonPressed;

    /// <summary>Occurs when a mapped button transitions to released.</summary>
    public event Action<IController, IButtonInput>? ButtonReleased;

    /// <summary>Occurs when a mapped analog position changes after normalization.</summary>
    public event Action<IController, IAnalogInput, Vector2D<float>>? AnalogChanged;

    /// <summary>Samples all controls, updates their state, then publishes captured transitions.</summary>
    public void Update()
    {
        if (!IsConnected)
            return;

        var buttonChanges = new List<(ButtonInput Button, bool IsPressed)>();
        foreach (var button in _buttons)
        {
            var isPressed = _readButton(button.Mapping.PhysicalButtonIndex);
            if (button.SetPressed(isPressed))
                buttonChanges.Add((button, isPressed));
        }

        var analogChanges = new List<(AnalogInputState Input, Vector2D<float> Position)>();
        for (var index = 0; index < _analogInputs.Length; index++)
        {
            var mapping = _analogMappings[index];
            var x = Normalize(
                _readAxis(mapping.PhysicalXAxisIndex),
                mapping.Normalization,
                mapping.InvertX
            );
            var y = mapping.PhysicalYAxisIndex is int yAxis
                ? Normalize(_readAxis(yAxis), mapping.Normalization, mapping.InvertY)
                : 0f;

            var position = new Vector2D<float>(x, y);
            if (_analogInputs[index].SetPosition(position))
                analogChanges.Add((_analogInputs[index], position));
        }

        foreach (var (button, isPressed) in buttonChanges)
        {
            if (isPressed)
                ButtonPressed?.Invoke(this, button);
            else
                ButtonReleased?.Invoke(this, button);
        }

        foreach (var (input, position) in analogChanges)
            AnalogChanged?.Invoke(this, input, position);
    }

    /// <summary>Clears cached control state and permanently marks the wrapper disconnected.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var button in _buttons)
            button.SetPressed(false);
        foreach (var input in _analogInputs)
            input.SetPosition(Vector2D<float>.Zero);
    }

    /// <summary>Normalizes a raw physical axis value into the profile's documented range.</summary>
    /// <param name="value">The raw physical value.</param>
    /// <param name="rule">The selected normalization rule.</param>
    /// <returns>The clamped normalized value.</returns>
    private static float Normalize(float value, AnalogNormalizationRule rule, bool inverted)
    {
        if (inverted)
        {
            value = rule switch
            {
                AnalogNormalizationRule.Signed => -value,
                AnalogNormalizationRule.UnipolarZeroToOne => 1f - value,
                AnalogNormalizationRule.UnipolarMinusOneToOne => -value,
                _ => throw new ArgumentOutOfRangeException(nameof(rule)),
            };
        }

        return rule switch
        {
            AnalogNormalizationRule.Signed => Math.Clamp(value, -1f, 1f),
            AnalogNormalizationRule.UnipolarZeroToOne => Math.Clamp(value, 0f, 1f),
            AnalogNormalizationRule.UnipolarMinusOneToOne => Math.Clamp(
                (value + 1f) * 0.5f,
                0f,
                1f
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
    }

    /// <summary>Stores state for one logical controller button.</summary>
    private sealed class ButtonInput(Controller controller, ButtonMapping mapping) : IButtonInput
    {
        private bool _isPressed;

        /// <summary>Gets the associated physical mapping.</summary>
        public ButtonMapping Mapping { get; } = mapping;

        /// <inheritdoc />
        public int Index => Mapping.LogicalIndex;

        /// <inheritdoc />
        public string? SemanticName => Mapping.SemanticName;

        /// <inheritdoc />
        public bool IsPressed => controller.IsConnected && _isPressed;

        /// <summary>Updates state and reports whether it changed.</summary>
        /// <param name="isPressed">The newly sampled state.</param>
        /// <returns><see langword="true"/> when the state changed.</returns>
        public bool SetPressed(bool isPressed)
        {
            if (_isPressed == isPressed)
                return false;
            _isPressed = isPressed;
            return true;
        }
    }

    /// <summary>Stores state for one logical analog input.</summary>
    private sealed class AnalogInputState(Controller controller, AnalogMapping mapping)
        : IAnalogInput
    {
        private Vector2D<float> _position;

        /// <inheritdoc />
        public int Index => mapping.LogicalIndex;

        /// <inheritdoc />
        public string? SemanticName => mapping.SemanticName;

        /// <inheritdoc />
        public Vector2D<float> Position =>
            controller.IsConnected ? _position : Vector2D<float>.Zero;

        /// <summary>Updates state and reports whether it changed.</summary>
        /// <param name="position">The newly sampled normalized position.</param>
        /// <returns><see langword="true"/> when the position changed.</returns>
        public bool SetPosition(Vector2D<float> position)
        {
            if (_position == position)
                return false;
            _position = position;
            return true;
        }
    }
}
