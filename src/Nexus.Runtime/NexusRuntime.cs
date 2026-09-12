namespace Nexus.Runtime;

/// <summary>
/// Coordinates the lifecycle and interaction of services participating
/// in a running Nexus game environment.
/// </summary>
public sealed class NexusRuntime(
    ITimingSource timing,
    IInputSystem input,
    ISceneGraph sceneGraph,
    IPhysicsSystem physics,
    IGraphicsSystem graphics,
    IAudioService audio
) : INexusRuntime
{
    private bool _initialized = false;
    public bool IsInitialized => _initialized;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Initializes the runtime and its configured services.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

        // Runtime initialization and subsystem orchestration will live here.

        _initialized = true;
        IsRunning = true;
    }

    /// <summary>
    /// Updates the runtime and all participating runtime systems.
    /// </summary>
    public void Update()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "The runtime must be initialized before it can be updated."
            );

        // This should be replaced by something else.
        // Silk.NET already handles frame rates and throttling,
        // So we shouldn't have to manage it here. We just
        // need to calculate deltaTime.

        // timing.Update();
        input.Update();

        sceneGraph.Update();

        physics.Update();
        graphics.Render();
        audio.Update();
    }

    public void Stop()
    {
        IsRunning = false;
    }
}
