using Nexus.Core.Performance;

namespace Nexus.Runtime;

/// <summary>
/// Coordinates the lifecycle and interaction of services participating
/// in a running Nexus game environment.
/// </summary>
/// <param name="eventHub">The event hub used to drain queued events.</param>
/// <param name="gameSystem">The game-system lifecycle coordinator.</param>
/// <param name="physics">The physics system.</param>
/// <param name="audio">The audio system.</param>
/// <param name="input">The input system.</param>
/// <param name="graphics">The graphics system.</param>
/// <param name="gui">The graphical user interface.</param>
/// <param name="sceneRegistry">The registry used to resolve the configured startup scene.</param>
/// <param name="sceneManager">The service holding pending scene transition requests.</param>
/// <param name="gameSettings">The settings containing the startup scene identifier.</param>
/// <param name="window">The optional window used for runtime callbacks.</param>
/// <param name="applicationSettings">The optional settings containing runtime limits.</param>
/// <param name="telemetry">The optional startup and performance telemetry sink.</param>
public sealed class NexusRuntime(
    IEventHub eventHub,
    IGameSystem gameSystem,
    IPhysicsSystem physics,
    IAudioSystem audio,
    IInputSystem input,
    IGraphicsSystem graphics,
    IGraphicalUserInterface gui,
    ISceneRegistry sceneRegistry,
    ISceneManager sceneManager,
    IOptions<GameSettings> gameSettings,
    IWindow? window = null,
    IOptions<ApplicationSettings>? applicationSettings = null,
    IPerformanceTelemetry? telemetry = null
) : INexusRuntime
{
    private bool _initialized = false;
    private readonly int _maxFrameCount = applicationSettings?.Value.MaxFrameCount ?? 0;
    private readonly TimeSpan _maxRunTime = applicationSettings?.Value.MaxRunTime ?? TimeSpan.Zero;
    private readonly Stopwatch _runtimeStopwatch = new();
    private long _renderedFrameCount;
    private bool _runTimeLimitReached;

    /// <summary>Gets whether the runtime and its configured services are initialized.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Initializes the runtime and its configured services.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No scene is registered, multiple scenes require an explicit start scene, or the configured scene is not registered.
    /// </exception>
    public void Initialize()
    {
        if (_initialized)
            return;

        using var startupTiming = new LoadPerformanceScope(telemetry, "startup.runtime.initialize");

        if (window is not null)
        {
            window.Update += OnUpdate;
            window.Render += OnRender;
        }

        input.Initialize();
        physics.Initialize();
        audio.Initialize();
        graphics.Initialize();
        gui.Initialize();
        gameSystem.Initialize();

        var startSceneId = gameSettings.Value?.StartSceneId;
        if (string.IsNullOrWhiteSpace(startSceneId))
        {
            if (sceneRegistry.SceneCount == 0)
                throw new InvalidOperationException(
                    "No scenes are registered. Register a scene before initializing the runtime."
                );

            if (sceneRegistry.SceneCount > 1)
                throw new InvalidOperationException(
                    $"Multiple scenes are registered ({string.Join(", ", sceneRegistry.RegisteredScenes)}). "
                        + "Game:StartSceneId configuration is required."
                );

            startSceneId = sceneRegistry.RegisteredScenes.Single();
        }

        sceneManager.LoadScene(startSceneId);

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

        if (sceneManager.IsSceneChangePending)
        {
            // Deliver removals queued by the outgoing scene before replacing it.
            eventHub.Drain();

            if (gameSystem.IsSceneLoaded)
            {
                gameSystem.UnloadScene();
                eventHub.Drain();
            }

            var newScene = sceneRegistry.Load(sceneManager.PendingSceneId);
            gameSystem.LoadScene(newScene);
        }

        eventHub.Drain();

        gameSystem.Update(deltaTime);
        // Newly activated scene components must reach graphics and GUI before this
        // frame's layout/rendering, rather than leaving a viewless transition frame.
        eventHub.Drain();
        physics.Update(deltaTime);
        gui.Update(deltaTime);
        audio.Update(deltaTime);
        input.Update(deltaTime);
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
