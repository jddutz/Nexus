namespace Nexus.Core;

/// <summary>
/// Settings that control diagnostic tooling and profiling behavior.
/// </summary>
public sealed record DiagnosticsSettings
{
    /// <summary>Enables diagnostic instrumentation while debugging the application.</summary>
    public bool Debugging { get; set; }

    /// <summary>Enables detailed graphics and resource diagnostics.</summary>
    public bool EnableDiagnostics { get; set; }

    /// <summary>Enables graphics timings and performance counters.</summary>
    public bool EnablePerformanceMetrics { get; set; }

    /// <summary>Gets whether performance measurements are enabled.</summary>
    public bool PerformanceInstrumentationEnabled => Debugging || EnablePerformanceMetrics;

    /// <summary>Gets whether graphics instrumentation is enabled by any application diagnostic flag.</summary>
    public bool GraphicsInstrumentationEnabled => Debugging || EnableDiagnostics || EnablePerformanceMetrics;

    /// <summary>
    /// Gets or sets a value indicating whether profiling is enabled.
    /// </summary>
    public bool EnableProfiling { get; set; } = false;
}
