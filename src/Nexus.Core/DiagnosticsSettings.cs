namespace Nexus.Core;

/// <summary>
/// Settings that control diagnostic tooling and profiling behavior.
/// </summary>
public sealed record DiagnosticsSettings(bool EnableProfiling = false);
