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
    /// Gets or sets whether diagnostic event logging is enabled.
    /// </summary>
    public bool DiagnosticsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether high-frequency events are included in diagnostic logs.
    /// </summary>
    public bool LogHighFrequencyEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of diagnostic event payloads logged per event type before suppression.
    /// </summary>
    public int EventLogLimit { get; set; } = 20;

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
