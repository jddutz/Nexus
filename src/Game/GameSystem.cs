namespace Nexus.Game;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
/// <param name="eventHub">The event hub used to register handlers and publish lifecycle events.</param>
/// <param name="logger">The logger used for game-system diagnostics.</param>
/// <param name="sceneRegistry">The registry used to load the configured initial scene.</param>
/// <param name="gameSettings">The settings bound from the Game configuration section.</param>
/// <param name="inputSystem">The optional input system assigned to active scenes.</param>
public class GameSystem(
    IEventHub eventHub,
    ILogger<GameSystem> logger,
    ISceneRegistry sceneRegistry,
    IOptions<GameSettings> gameSettings,
    IInputSystem? inputSystem = null
) : IGameSystem
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly IInputSystem? _inputSystem = inputSystem;
    private readonly ILogger<GameSystem> _logger = logger;
    private readonly ISceneRegistry _sceneRegistry = sceneRegistry;

    /// <summary>Gets the settings bound to the Game configuration section.</summary>
    public GameSettings Settings { get; } = gameSettings.Value;

    /// <summary>
    /// Gets the configured initial scene after initialization, or <see langword="null"/> beforehand.
    /// </summary>
    public IScene? InitialScene { get; private set; }

    /// <summary>
    /// Gets the currently active scene.
    /// </summary>
    private IScene? _currentScene;

    /// <summary>
    /// Gets the currently active scene.
    /// </summary>
    public IScene? CurrentScene
    {
        get => _currentScene;
        private set
        {
            var previousScene = _currentScene;

            _currentScene?.ComponentAdded -= ActivateComponent;
            _currentScene?.ComponentRemoved -= DeactivateComponent;
            _currentScene?.GameObjectAdded -= ActivateGameObject;
            _currentScene?.GameObjectRemoved -= DeactivateGameObject;

            if (previousScene is not null)
                _eventHub.Publish(new SceneUnloadedEvent(previousScene));

            _currentScene = value;

            if (_currentScene is Scene scene)
                scene.SetInputSystem(_inputSystem);

            if (_currentScene != null)
            {
                _currentScene.ComponentAdded += ActivateComponent;
                _currentScene.ComponentRemoved += DeactivateComponent;
                _currentScene.GameObjectAdded += ActivateGameObject;
                _currentScene.GameObjectRemoved += DeactivateGameObject;
                _eventHub.Publish(new SceneLoadedEvent(_currentScene));
            }

            _logger.LogInformation(
                "Active scene changed. PreviousSceneType={PreviousSceneType}, CurrentSceneType={CurrentSceneType}",
                previousScene?.GetType().Name ?? "None",
                _currentScene?.GetType().Name ?? "None"
            );
        }
    }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    public void Initialize()
    {
        var initialSceneId = (SceneId)Settings.InitialScene;
        var initialScene =
            _sceneRegistry.Load(initialSceneId)
            ?? throw new InvalidOperationException(
                $"Initial scene '{Settings.InitialScene}' is not registered."
            );
        InitialScene = initialScene;

        _logger.LogInformation(
            "Initializing game system. InitialSceneType={InitialSceneType}",
            initialScene.GetType().Name
        );

        initialScene.Initialize();
        CurrentScene = initialScene;

        _logger.LogTrace("Activating scene...");
        initialScene.Activate();
        foreach (var child in initialScene.Children.OfType<IGameObject>())
            ActivateSubtree(child);
        _logger.LogTrace("Scene activation complete.");
        _logger.LogInformation("Game system initialized and initial scene activated.");
    }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime)
    {
        if (CurrentScene is null)
            return;

        foreach (var child in CurrentScene.Children.OfType<IGameObject>())
            UpdateGameObject(child, deltaTime);
    }

    /// <summary>Publishes a component activation event.</summary>
    /// <param name="component">The component to activate.</param>
    public void ActivateComponent(IComponent component)
    {
        component.Initialize();
        component.Activate();
        _eventHub.Register(component);

        _logger.LogTrace(
            "Activating component. ComponentType={ComponentType}",
            component.GetType().Name
        );

        _eventHub.Publish(new ComponentActivatedEvent(component));
    }

    /// <summary>Publishes a component deactivation event.</summary>
    /// <param name="component">The component to deactivate.</param>
    public void DeactivateComponent(IComponent component)
    {
        _eventHub.Unregister(component);
        if (!component.IsActivated)
            return;

        component.Deactivate();

        _logger.LogTrace(
            "Deactivating component. ComponentType={ComponentType}",
            component.GetType().Name
        );

        _eventHub.Publish(new ComponentDeactivatedEvent(component));
    }

    /// <summary>Publishes a game-object activation event.</summary>
    /// <param name="gameObject">The game object to activate.</param>
    public void ActivateGameObject(IGameObject gameObject)
    {
        gameObject.Initialize();
        if (!gameObject.CanActivate())
            return;

        gameObject.Activate();
        RegisterEventHandlers(gameObject);

        _logger.LogTrace(
            "Activating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        _eventHub.Publish(new GameObjectActivatedEvent(gameObject));
    }

    /// <summary>Publishes a game-object deactivation event.</summary>
    /// <param name="gameObject">The game object to deactivate.</param>
    public void DeactivateGameObject(IGameObject gameObject)
    {
        UnregisterEventHandlers(gameObject);
        if (gameObject.IsActivated)
            gameObject.Deactivate();

        _logger.LogTrace(
            "Deactivating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        _eventHub.Publish(new GameObjectDeactivatedEvent(gameObject));
    }

    /// <summary>
    /// Initializes and activates a game object subtree in parent-first order.
    /// </summary>
    /// <param name="gameObject">The root game object to activate.</param>
    private void ActivateSubtree(IGameObject gameObject)
    {
        ActivateGameObject(gameObject);
        if (!gameObject.IsActivated)
            return;

        foreach (var component in gameObject.Components)
            ActivateComponent(component);

        foreach (var child in gameObject.Children.OfType<IGameObject>())
            ActivateSubtree(child);
    }

    /// <summary>
    /// Updates a game object and its descendants in parent-first order.
    /// </summary>
    /// <param name="gameObject">The root game object to update.</param>
    /// <param name="deltaTime">The elapsed time in seconds since the previous update.</param>
    private static void UpdateGameObject(IGameObject gameObject, double deltaTime)
    {
        gameObject.Update(deltaTime);
        foreach (var child in gameObject.Children.OfType<IGameObject>())
            UpdateGameObject(child, deltaTime);
    }

    /// <summary>
    /// Registers a game object as a global event handler.
    /// </summary>
    /// <param name="gameObject">The root game object to register.</param>
    private void RegisterEventHandlers(IGameObject gameObject)
    {
        _eventHub.Register(gameObject);
    }

    /// <summary>
    /// Unregisters a game object from global event handling.
    /// </summary>
    /// <param name="gameObject">The root game object to unregister.</param>
    private void UnregisterEventHandlers(IGameObject gameObject)
    {
        _eventHub.Unregister(gameObject);
    }
}
