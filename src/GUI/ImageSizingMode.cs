namespace Nexus.GUI;

/// <summary>Specifies how content is sized within its available bounds.</summary>
public enum ImageSizingMode
{
    /// <summary>Uses the content's original size.</summary>
    Original,

    /// <summary>Scales content to fit within the bounds while preserving its aspect ratio.</summary>
    Fit,

    /// <summary>Scales content to fit the available width while preserving its aspect ratio.</summary>
    FitHorizontal,

    /// <summary>Scales content to fit the available height while preserving its aspect ratio.</summary>
    FitVertical,

    /// <summary>Scales content to cover the bounds while preserving its aspect ratio.</summary>
    Fill,

    /// <summary>Scales content to match the bounds without preserving its aspect ratio.</summary>
    Stretch,

    /// <summary>Uses sizing specified by the caller.</summary>
    Custom,
}
