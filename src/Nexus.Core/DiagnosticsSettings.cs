namespace Nexus.Core;

/// <summary>
/// Settings that control diagnostic tooling and profiling behavior.
/// </summary>
public sealed record DiagnosticsSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether profiling is enabled.
    /// </summary>
    public bool EnableProfiling { get; set; } = false;
}
