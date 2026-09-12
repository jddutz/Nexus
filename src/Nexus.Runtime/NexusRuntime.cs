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
    IAudioSystem audio
) : INexusRuntime
{
    private bool _initialized = false;
    private TimeSpan _previousElapsed;
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

        _previousElapsed = timing.Elapsed;
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

        var elapsed = timing.Elapsed;
        var deltaTime = elapsed - _previousElapsed;
        _previousElapsed = elapsed;

        input.Update(deltaTime);

        sceneGraph.Update(deltaTime);

        physics.Update(deltaTime);
        graphics.Render();
        audio.Update(deltaTime);
    }

    public void Stop()
    {
        IsRunning = false;
    }
}
