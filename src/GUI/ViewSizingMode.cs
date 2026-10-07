namespace Nexus.GUI;

/// <summary>Specifies how a view's camera projection is mapped into its arranged bounds.</summary>
public enum ViewSizingMode
{
    /// <summary>Preserves the projection aspect ratio and keeps the complete projection visible.</summary>
    Fit,

    /// <summary>Preserves the projection aspect ratio and fills the bounds, clipping overflow.</summary>
    Fill,

    /// <summary>Maps the projection to the bounds without preserving its aspect ratio.</summary>
    Stretch,
}

