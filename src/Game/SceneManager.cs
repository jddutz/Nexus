namespace Nexus.Game;

/// <summary>
/// Stores the latest pending scene transition request.
/// </summary>
public sealed class SceneManager : ISceneManager
{
    private readonly object _syncRoot = new();
    private readonly ILogger<SceneManager> _logger;
    private string? _pendingSceneId;

    /// <summary>Creates a scene manager and registers it for scene-loaded events.</summary>
    /// <param name="eventHub">The event hub used to observe loaded scenes.</param>
    /// <param name="logger">The logger used to report scene ID mismatches.</param>
    public SceneManager(IEventHub eventHub, ILogger<SceneManager> logger)
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        eventHub.Register(this);
    }

    /// <inheritdoc />
    public bool IsSceneChangePending
    {
        get
        {
            lock (_syncRoot)
                return _pendingSceneId is not null;
        }
    }

    /// <inheritdoc />
    public string PendingSceneId
    {
        get
        {
            lock (_syncRoot)
                return _pendingSceneId ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public void LoadScene<TScene>()
        where TScene : IScene
    {
        var sceneId =
            typeof(TScene).GetCustomAttribute<SceneAttribute>(inherit: false)?.Name
            ?? typeof(TScene).Name;

        LoadScene(sceneId);
    }

    /// <inheritdoc />
    public void LoadScene(string sceneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);

        lock (_syncRoot)
        {
            _pendingSceneId = sceneId;
        }
    }

    /// <summary>
    /// Clears the pending request when a scene finishes loading and warns if it differs from the request.
    /// </summary>
    /// <param name="message">The scene-loaded event.</param>
    public void Handle(SceneLoadedEvent message)
    {
        var actualSceneId =
            message.Scene.GetType().GetCustomAttribute<SceneAttribute>(inherit: false)?.Name
            ?? message.Scene.GetType().Name;

        lock (_syncRoot)
        {
            if (_pendingSceneId is null)
            {
                _logger.LogWarning(
                    "SceneLoadedEvent raised without a pending scene change request. LoadedSceneId={LoadedSceneId}",
                    actualSceneId
                );

                return;
            }

            if (!string.Equals(_pendingSceneId, actualSceneId, StringComparison.Ordinal))
                _logger.LogWarning(
                    "Loaded scene ID does not match the pending scene request. RequestedSceneId={RequestedSceneId}, LoadedSceneId={LoadedSceneId}",
                    _pendingSceneId,
                    actualSceneId
                );

            _pendingSceneId = null;
        }
    }
}
