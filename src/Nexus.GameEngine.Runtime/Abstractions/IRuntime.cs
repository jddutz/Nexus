namespace Nexus.GameEngine.Runtime.Abstractions;

/// <summary>
/// Represents a configured Nexus runtime environment.
/// Coordinates the lifecycle and execution of the engine systems
/// participating in the runtime.
/// </summary>
public interface IRuntime : IDisposable
{
    /// <summary>
    /// Gets the service provider containing the configured runtime services.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets whether the runtime has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets whether the runtime is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Initializes the runtime and its configured systems.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Advances the runtime by one update cycle.
    /// </summary>
    void Update();

    /// <summary>
    /// Stops the runtime and begins orderly shutdown of its
    /// configured systems.
    /// </summary>
    void Stop();
}
