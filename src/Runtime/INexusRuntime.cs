namespace Nexus.Runtime;

/// <summary>
/// Represents a configured Nexus runtime environment.
/// Coordinates the lifecycle and execution of the engine systems
/// participating in the runtime.
/// </summary>
public interface INexusRuntime
{
    /// <summary>
    /// Gets whether the runtime has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes the runtime and its configured systems.
    /// </summary>
    void Initialize();
}
