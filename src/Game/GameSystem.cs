namespace Nexus.Game;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem(
    IEventHub eventHub,
    ILogger<GameSystem> logger,
    IInputSystem? inputSystem = null
) : IGameSystem, IGameModel
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly IInputSystem? _inputSystem = inputSystem;
    private readonly ILogger<GameSystem> _logger = logger;
    private readonly Dictionary<GameObjectId, IGameObject> _gameObjects = [];

    /// <summary>
    /// Gets or sets the scene activated when the game starts.
    /// </summary>
    public IScene InitialScene { get; set; } = new Scene();

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

    /// <inheritdoc/>
    public IGameObject? GetGameObject(GameObjectId gameObjectId)
    {
        return _gameObjects.GetValueOrDefault(gameObjectId);
    }

    /// <inheritdoc/>
    public void RegisterGameObject(IGameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        _gameObjects[gameObject.Id] = gameObject;
        RegisterEventHandlers(gameObject);
    }

    /// <inheritdoc/>
    public void UnregisterGameObject(IGameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        _gameObjects.Remove(gameObject.Id);
        UnregisterEventHandlers(gameObject);
    }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    public void Initialize()
    {
        _logger.LogInformation(
            "Initializing game system. InitialSceneType={InitialSceneType}",
            InitialScene.GetType().Name
        );

        InitialScene.SetGameModel(this);
        CurrentScene = InitialScene;

        _logger.LogTrace("Activating scene...");
        CurrentScene.Activate();
        _logger.LogTrace("Scene activation complete.");
        _logger.LogInformation("Game system initialized and initial scene activated.");
    }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime)
    {
        CurrentScene?.Update(deltaTime);
    }

    /// <summary>Publishes a component activation event.</summary>
    /// <param name="component">The component to activate.</param>
    public void ActivateComponent(IComponent component)
    {
        component.IsActivated = true;
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
        component.IsActivated = false;
        _eventHub.Unregister(component);

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

        _logger.LogTrace(
            "Deactivating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        _eventHub.Publish(new GameObjectDeactivatedEvent(gameObject));
    }

    /// <summary>
    /// Registers a game object and its current subtree as global event handlers.
    /// </summary>
    /// <param name="gameObject">The root game object to register.</param>
    private void RegisterEventHandlers(IGameObject gameObject)
    {
        _eventHub.Register(gameObject);

        foreach (var component in gameObject.Components)
            _eventHub.Register(component);

        foreach (var child in gameObject.Children)
            RegisterEventHandlers(child);
    }

    /// <summary>
    /// Unregisters a game object and its current subtree from global event handling.
    /// </summary>
    /// <param name="gameObject">The root game object to unregister.</param>
    private void UnregisterEventHandlers(IGameObject gameObject)
    {
        _eventHub.Unregister(gameObject);

        foreach (var component in gameObject.Components)
            _eventHub.Unregister(component);

        foreach (var child in gameObject.Children)
            UnregisterEventHandlers(child);
    }
}
