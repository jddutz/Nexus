namespace Nexus.Input.Devices;

using System.Collections.ObjectModel;

/// <summary>Defines physical-to-logical control mappings for a controller source. Signed axes retain a centered zero range of -1 through +1; one-direction controls use an explicit unipolar normalization rule.</summary>
public sealed class DeviceProfile
{
    private readonly ReadOnlyCollection<ButtonMapping> _buttons;
    private readonly ReadOnlyCollection<AnalogMapping> _analogInputs;

    /// <summary>Gets ordered logical button mappings.</summary>
    public IReadOnlyList<ButtonMapping> Buttons => _buttons;

    /// <summary>Gets ordered logical analog mappings.</summary>
    public IReadOnlyList<AnalogMapping> AnalogInputs => _analogInputs;

    /// <summary>Creates an immutable profile after checking logical indices and axis ownership.</summary>
    /// <param name="buttons">Button mappings ordered by logical index.</param>
    /// <param name="analogInputs">Analog mappings ordered by logical index.</param>
    public DeviceProfile(
        IEnumerable<ButtonMapping>? buttons = null,
        IEnumerable<AnalogMapping>? analogInputs = null
    )
    {
        var buttonMappings = (buttons ?? []).OrderBy(mapping => mapping.LogicalIndex).ToArray();
        var analogMappings = (analogInputs ?? [])
            .OrderBy(mapping => mapping.LogicalIndex)
            .ToArray();
        ValidateLogicalIndices(
            buttonMappings.Select(mapping => mapping.LogicalIndex),
            nameof(buttons)
        );
        ValidateLogicalIndices(
            analogMappings.Select(mapping => mapping.LogicalIndex),
            nameof(analogInputs)
        );

        var axes = new HashSet<int>();
        foreach (var mapping in analogMappings)
        {
            if (!Enum.IsDefined(mapping.Normalization))
                throw new ArgumentOutOfRangeException(
                    nameof(analogInputs),
                    "The normalization rule is not defined."
                );
            if (mapping.PhysicalXAxisIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(analogInputs),
                    "Axis indices must be non-negative."
                );
            if (!axes.Add(mapping.PhysicalXAxisIndex))
                throw new ArgumentException(
                    "A physical axis may belong to only one analog input.",
                    nameof(analogInputs)
                );
            if (mapping.PhysicalYAxisIndex is int yAxis)
            {
                if (yAxis < 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(analogInputs),
                        "Axis indices must be non-negative."
                    );
                if (!axes.Add(yAxis))
                    throw new ArgumentException(
                        "A physical axis may belong to only one analog input.",
                        nameof(analogInputs)
                    );
            }
        }

        foreach (var mapping in buttonMappings)
        {
            if (mapping.PhysicalButtonIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(buttons),
                    "Button indices must be non-negative."
                );
        }

        _buttons = Array.AsReadOnly(buttonMappings);
        _analogInputs = Array.AsReadOnly(analogMappings);
    }

    /// <summary>Validates physical indices against a source's reported control counts.</summary>
    /// <param name="physicalButtonCount">Number of addressable physical button indices.</param>
    /// <param name="physicalAxisCount">Number of addressable physical axis indices.</param>
    /// <exception cref="ArgumentOutOfRangeException">A mapping references a missing physical control.</exception>
    public void Validate(int physicalButtonCount, int physicalAxisCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(physicalButtonCount);
        ArgumentOutOfRangeException.ThrowIfNegative(physicalAxisCount);

        foreach (var mapping in _buttons)
        {
            if (mapping.PhysicalButtonIndex >= physicalButtonCount)
                throw new ArgumentOutOfRangeException(
                    nameof(physicalButtonCount),
                    mapping.PhysicalButtonIndex,
                    "Button mapping is outside the physical source."
                );
        }

        foreach (var mapping in _analogInputs)
        {
            if (mapping.PhysicalXAxisIndex >= physicalAxisCount)
                throw new ArgumentOutOfRangeException(
                    nameof(physicalAxisCount),
                    mapping.PhysicalXAxisIndex,
                    "Axis mapping is outside the physical source."
                );
            if (mapping.PhysicalYAxisIndex is int yAxis && yAxis >= physicalAxisCount)
                throw new ArgumentOutOfRangeException(
                    nameof(physicalAxisCount),
                    yAxis,
                    "Axis mapping is outside the physical source."
                );
        }
    }

    /// <summary>Validates that logical indices are unique, contiguous, and start at zero.</summary>
    /// <param name="indices">The configured logical indices.</param>
    /// <param name="parameterName">The profile constructor parameter name.</param>
    private static void ValidateLogicalIndices(IEnumerable<int> indices, string parameterName)
    {
        var ordered = indices.Order().ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index] != index)
                throw new ArgumentException(
                    "Logical indices must be unique, contiguous, and start at zero.",
                    parameterName
                );
        }
    }
}

/// <summary>Maps one logical button to a physical button index.</summary>
/// <param name="LogicalIndex">The contiguous controller-local index.</param>
/// <param name="PhysicalButtonIndex">The source button index.</param>
/// <param name="SemanticName">The optional normalized physical role.</param>
public sealed record ButtonMapping(
    int LogicalIndex,
    int PhysicalButtonIndex,
    string? SemanticName = null
);

/// <summary>Specifies how raw physical axis values are normalized.</summary>
public enum AnalogNormalizationRule
{
    /// <summary>Clamp centered values to -1 through +1.</summary>
    Signed,

    /// <summary>Clamp values already released at zero to zero through +1.</summary>
    UnipolarZeroToOne,

    /// <summary>Convert -1 through +1 values to zero through +1.</summary>
    UnipolarMinusOneToOne,
}

/// <summary>Maps one logical analog input to one or two physical axes.</summary>
/// <param name="LogicalIndex">The contiguous controller-local index.</param>
/// <param name="PhysicalXAxisIndex">The source axis mapped to X.</param>
/// <param name="PhysicalYAxisIndex">The optional source axis mapped to Y.</param>
/// <param name="SemanticName">The optional normalized physical role.</param>
/// <param name="InvertX">Whether the X value is inverted.</param>
/// <param name="InvertY">Whether the Y value is inverted.</param>
/// <param name="Normalization">The physical source's raw-value normalization rule.</param>
public sealed record AnalogMapping(
    int LogicalIndex,
    int PhysicalXAxisIndex,
    int? PhysicalYAxisIndex = null,
    string? SemanticName = null,
    bool InvertX = false,
    bool InvertY = false,
    AnalogNormalizationRule Normalization = AnalogNormalizationRule.Signed
);
