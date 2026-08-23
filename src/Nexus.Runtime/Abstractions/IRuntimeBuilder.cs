namespace Nexus.Runtime.Abstractions;

/// <summary>
/// Defines a builder capable of constructing a configured Nexus runtime.
/// </summary>
public interface IRuntimeBuilder
{
    /// <summary>
    /// Builds the configured runtime.
    /// </summary>
    /// <returns>
    /// A fully configured runtime instance.
    /// </returns>
    IRuntime Build();
}
