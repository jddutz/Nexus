namespace Nexus.Runtime.Settings;

/// <summary>
/// Settings that describe the application hosted by the Nexus runtime.
/// </summary>
public sealed class ApplicationSettings
{
    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public string ApplicationVersion { get; set; } = string.Empty;
}
