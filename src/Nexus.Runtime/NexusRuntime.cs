namespace Nexus.Runtime;

/// <summary>
/// Coordinates the lifecycle and interaction of services participating
/// in a running Nexus game environment.
/// </summary>
public sealed class NexusRuntime(
    IWindow window,
    IInputSystem input,
    ISceneGraph sceneGraph,
    IPhysicsSystem physics,
    IGraphicsSystem graphics,
    IAudioSystem audio
) : INexusRuntime
{
    private bool _initialized = false;
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Initializes the runtime and its configured services.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

        // Runtime initialization and subsystem orchestration will live here.
        window.Update += OnUpdate;
        window.Render += OnRender;

        _initialized = true;
    }

    /// <summary>
    /// Updates the runtime and all participating runtime systems.
    /// </summary>
    public void OnUpdate(double deltaTime)
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "The runtime must be initialized before it can be updated."
            );

        input.Update(deltaTime);
        sceneGraph.Update(deltaTime);
        physics.Update(deltaTime);
        audio.Update(deltaTime);
    }

    public void OnRender(double deltaTime)
    {
        graphics.Render();
    }
}
