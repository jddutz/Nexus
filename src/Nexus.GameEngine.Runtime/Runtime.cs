namespace Nexus.GameEngine.Runtime;

/// <summary>
/// Coordinates the lifecycle and interaction of services participating
/// in a running Nexus game environment.
/// </summary>
public sealed class Runtime : IDisposable
{
    private readonly IServiceProvider _services;
    private bool _initialized;
    private bool _disposed;

    public Runtime(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
    }

    /// <summary>
    /// Initializes the runtime and its configured services.
    /// </summary>
    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized)
            return;

        // Runtime initialization and subsystem orchestration will live here.

        _initialized = true;
    }

    /// <summary>
    /// Updates the runtime and all participating runtime systems.
    /// </summary>
    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
            throw new InvalidOperationException(
                "The runtime must be initialized before it can be updated."
            );

        // Timing, scene, physics, and other update orchestration will live here.
    }

    /// <summary>
    /// Shuts down the runtime and releases its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Runtime shutdown and subsystem teardown will live here.

        if (_services is IDisposable disposable)
            disposable.Dispose();

        _disposed = true;
    }
}
