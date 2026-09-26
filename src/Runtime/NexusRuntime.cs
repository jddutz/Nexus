using System.Diagnostics;
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
    private readonly TimeSpan _maxRunTime = applicationSettings?.Value.MaxRunTime ?? TimeSpan.Zero;
    private readonly Stopwatch _runtimeStopwatch = new();
    private long _renderedFrameCount;
    private bool _runTimeLimitReached;

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

        _runtimeStopwatch.Start();
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
    /// Renders a frame and requests window shutdown when a configured runtime limit is reached.
    /// </summary>
    /// <param name="deltaTime">Elapsed time since the previous frame.</param>
    public void OnRender(double deltaTime)
    {
        graphics.Render();

        if (_maxFrameCount > 0 && ++_renderedFrameCount == _maxFrameCount)
        {
            window?.Close();
            return;
        }

        if (_maxRunTime <= TimeSpan.Zero || _runTimeLimitReached)
            return;

        if (_runtimeStopwatch.Elapsed >= _maxRunTime)
        {
            _runTimeLimitReached = true;
            window?.Close();
        }
    }
}
