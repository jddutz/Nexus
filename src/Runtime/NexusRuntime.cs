using Nexus.Core.Events;

namespace Nexus.Runtime;

/// <summary>
/// Coordinates the lifecycle and interaction of services participating
/// in a running Nexus game environment.
/// </summary>
public sealed class NexusRuntime(
    IEventHub eventHub,
    IGameSystem gameSystem,
    IPhysicsSystem physics,
    IAudioSystem audio,
    IInputSystem input,
    IGraphicsSystem graphics,
    IWindow? window = null,
    IOptions<ApplicationSettings>? applicationSettings = null
) : INexusRuntime
{
    private bool _initialized = false;
    private readonly int _maxFrameCount = applicationSettings?.Value.MaxFrameCount ?? 0;
    private long _renderedFrameCount;

    public bool IsInitialized => _initialized;

    /// <summary>
    /// Initializes the runtime and its configured services.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

        if (window is not null)
        {
            window.Update += OnUpdate;
            window.Render += OnRender;
        }

        physics.Initialize();
        audio.Initialize();
        input.Initialize();
        gameSystem.Initialize();
        graphics.Initialize();

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
        //graphics.Update(deltaTime);
    }

    /// <summary>
    /// Renders a frame and requests window shutdown when the configured frame limit is reached.
    /// </summary>
    /// <param name="deltaTime">Elapsed time since the previous frame.</param>
    public void OnRender(double deltaTime)
    {
        graphics.Render();

        if (_maxFrameCount <= 0)
            return;

        _renderedFrameCount++;
        if (_renderedFrameCount == _maxFrameCount)
            window?.Close();
    }
}
