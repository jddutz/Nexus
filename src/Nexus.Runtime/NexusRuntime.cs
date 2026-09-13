using Nexus.Core.Events;

namespace Nexus.Runtime;

/// <summary>
/// Coordinates the lifecycle and interaction of services participating
/// in a running Nexus game environment.
/// </summary>
public sealed class NexusRuntime(
    IEventHub eventHub,
    IInputSystem input,
    IGameSystem gameSystem,
    IPhysicsSystem physics,
    IGraphicsSystem graphics,
    IAudioSystem audio,
    IWindow? window = null
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
        if (window is not null)
        {
            window.Update += OnUpdate;
            window.Render += OnRender;
        }

        gameSystem.Initialize();

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

        eventHub.Drain();

        gameSystem.Update(deltaTime);
        physics.Update(deltaTime);
        audio.Update(deltaTime);
        input.Update(deltaTime);
    }

    public void OnRender(double deltaTime)
    {
        graphics.Render();
    }
}
