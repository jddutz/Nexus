namespace Nexus.GUI;

/// <summary>Defines the sizing mode and value for a grid row or column track.</summary>
public readonly record struct GridSize
{
    /// <summary>Initializes an absolute zero-length grid track.</summary>
    public GridSize()
        : this(GridSizeMode.Absolute, 0f) { }

    /// <summary>Initializes a grid track with an optional value.</summary>
    /// <param name="mode">The sizing mode for the track.</param>
    /// <param name="value">
    /// The absolute length or relative weight, defaulting to zero.
    /// Relative tracks must use a positive value.
    /// </param>
    public GridSize(GridSizeMode mode, float value = 0f)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(nameof(value));

        if (mode == GridSizeMode.Relative && value == 0f)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Relative weight must be greater than zero."
            );

        Mode = mode;
        Value = value;
    }

    /// <summary>Gets the sizing mode of the track.</summary>
    public GridSizeMode Mode { get; }

    /// <summary>Gets the absolute length or relative weight of the track.</summary>
    public float Value { get; }

    /// <summary>Creates an absolute-length grid track.</summary>
    /// <param name="size">The non-negative track length.</param>
    public static GridSize Absolute(float size) => new(GridSizeMode.Absolute, size);

    /// <summary>Creates a relative-weight grid track.</summary>
    /// <param name="weight">The positive track weight.</param>
    public static GridSize Relative(float weight = 1f) => new(GridSizeMode.Relative, weight);
}
