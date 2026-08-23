namespace Nexus.Runtime.Abstractions;

/// <summary>
/// Defines a host capable of running a configured Nexus runtime.
/// </summary>
public interface IApplication
{
    /// <summary>
    /// Starts the application host and runs the configured runtime
    /// until execution is terminated.
    /// </summary>
    void Run();
}
