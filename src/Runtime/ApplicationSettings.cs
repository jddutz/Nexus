namespace Nexus.Runtime;

/// <summary>
/// Settings that describe the application hosted by the Nexus runtime.
/// </summary>
public sealed record ApplicationSettings
{
    /// <summary>
    /// Gets or sets the display name of the application.
    /// </summary>
    public string ApplicationName { get; set; } = "Nexus Application";

    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public string ApplicationVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the location of the content manifest.
    /// </summary>
    public string ContentManifestLocation { get; set; } = ".content/content-manifest.json";

    /// <summary>
    /// Gets or sets the maximum number of frames to render before requesting application shutdown.
    /// A value less than or equal to zero disables the limit.
    /// </summary>
    public int MaxFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum runtime before requesting application shutdown.
    /// A value less than or equal to zero disables the limit.
    /// </summary>
    public TimeSpan MaxRunTime { get; set; }
}
