namespace Nexus.GUI;

/// <summary>Defines minimum and maximum dimensions for a flow-layout item.</summary>
public readonly record struct ItemSize
{
    /// <summary>Gets the default size with zero minimums and unbounded maximums.</summary>
    public static ItemSize Unbounded => new();

    /// <summary>Initializes an item size with zero minimums and unbounded maximums.</summary>
    public ItemSize()
        : this(
            minimumHeight: 0f,
            maximumHeight: float.MaxValue,
            minimumWidth: 0f,
            maximumWidth: float.MaxValue,
            maintainAspectRatio: false
        ) { }

    /// <summary>Initializes an item size with optional height and width bounds.</summary>
    /// <param name="minimumHeight">The minimum item height, defaulting to zero.</param>
    /// <param name="maximumHeight">The maximum item height, defaulting to unbounded.</param>
    /// <param name="minimumWidth">The minimum item width, defaulting to zero.</param>
    /// <param name="maximumWidth">The maximum item width, defaulting to unbounded.</param>
    /// <param name="maintainAspectRatio">Whether item sizing preserves its aspect ratio.</param>
    public ItemSize(
        float minimumHeight = 0f,
        float maximumHeight = float.MaxValue,
        float minimumWidth = 0f,
        float maximumWidth = float.MaxValue,
        bool maintainAspectRatio = false
    )
    {
        if (!float.IsFinite(minimumHeight) || minimumHeight < 0f)
            throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        if (!float.IsFinite(maximumHeight) || maximumHeight < minimumHeight)
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        if (!float.IsFinite(minimumWidth) || minimumWidth < 0f)
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        if (!float.IsFinite(maximumWidth) || maximumWidth < minimumWidth)
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));

        MinimumHeight = minimumHeight;
        MaximumHeight = maximumHeight;
        MinimumWidth = minimumWidth;
        MaximumWidth = maximumWidth;
        MaintainAspectRatio = maintainAspectRatio;
    }

    /// <summary>Gets the minimum permitted height.</summary>
    public float MinimumHeight { get; }

    /// <summary>Gets the maximum permitted height.</summary>
    public float MaximumHeight { get; }

    /// <summary>Gets the minimum permitted width.</summary>
    public float MinimumWidth { get; }

    /// <summary>Gets the maximum permitted width.</summary>
    public float MaximumWidth { get; }

    /// <summary>Gets whether item sizing preserves its aspect ratio.</summary>
    public bool MaintainAspectRatio { get; }
}
