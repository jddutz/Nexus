namespace Nexus.GUI;

/// <summary>Defines horizontal and vertical spacing between flow-layout items.</summary>
public readonly record struct ItemSpacing
{
    /// <summary>Gets an item spacing value with no spacing in either direction.</summary>
    public static ItemSpacing None => new();

    /// <summary>Initializes item spacing with no spacing in either direction.</summary>
    public ItemSpacing()
        : this(horizontal: 0f, vertical: 0f) { }

    /// <summary>Initializes item spacing with optional horizontal and vertical values.</summary>
    /// <param name="horizontal">The horizontal spacing, defaulting to zero.</param>
    /// <param name="vertical">The vertical spacing, defaulting to zero.</param>
    public ItemSpacing(float horizontal = 0f, float vertical = 0f)
    {
        if (!float.IsFinite(horizontal) || horizontal < 0f)
            throw new ArgumentOutOfRangeException(nameof(horizontal));
        if (!float.IsFinite(vertical) || vertical < 0f)
            throw new ArgumentOutOfRangeException(nameof(vertical));

        Horizontal = horizontal;
        Vertical = vertical;
    }

    /// <summary>Gets the horizontal spacing.</summary>
    public float Horizontal { get; }

    /// <summary>Gets the vertical spacing.</summary>
    public float Vertical { get; }
}
