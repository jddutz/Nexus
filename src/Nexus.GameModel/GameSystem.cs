namespace Nexus.GameModel;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem(
    IGraphicsSystem graphics,
    IPhysicsSystem physics,
    IAudioSystem audio,
    ILogger<GameSystem> logger,
    ILoggerFactory loggerFactory
) : IGameSystem
{
    private readonly ILogger<GameSystem> _logger = logger;

    /// <summary>
    /// Gets or sets the identifier of the scene activated when the game starts.
    /// </summary>
    public GameObjectId InitialSceneId { get; set; } = new GameObjectId(1);

    /// <summary>
    /// Gets the currently active scene.
    /// </summary>
    private IScene? _currentScene;
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

            _currentScene = value;

            if (_currentScene != null)
            {
                _currentScene.ComponentAdded += ActivateComponent;
                _currentScene.ComponentRemoved += DeactivateComponent;
                _currentScene.GameObjectAdded += ActivateGameObject;
                _currentScene.GameObjectRemoved += DeactivateGameObject;
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
        _logger.LogInformation(
            "Initializing game system. InitialSceneId={InitialSceneId}",
            InitialSceneId
        );

        if (InitialSceneId == GameObjectId.Invalid)
        {
            _logger.LogError(
                "Game system initialization failed because the initial scene is invalid."
            );
            throw new InvalidOperationException("Initial Scene is not defined.");
        }

        CurrentScene = new Scene(loggerFactory.CreateLogger<Scene>());

        var background = CurrentScene.AddComponent<UniformColorMeshRenderer>();

        background.Color = Colors.Red;
        background.Geometry = new VertexGeometryResourceDescription(
            "Background",
            [new(-1f, -1f, 0f), new(3f, -1f, 0f), new(-1f, 3f, 0f)]
        );

        CurrentScene.Activate();
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

    /// <summary>
    /// Activates a component across the graphics, physics, and audio systems.
    /// </summary>
    /// <param name="component">The component to activate.</param>
    public void ActivateComponent(IComponent component)
    {
        _logger.LogDebug(
            "Activating component. ComponentType={ComponentType}",
            component.GetType().Name
        );

        switch (component)
        {
            case IGraphicsComponent graphicsComponent:
                graphics.Activate(graphicsComponent);
                return;
            case IAudioComponent audioComponent:
                audio.Activate(audioComponent);
                return;
            case IPhysicsComponent physicsComponent:
                physics.Activate(physicsComponent);
                return;
        }
    }

    /// <summary>
    /// Deactivates a component across the graphics, physics, and audio systems.
    /// </summary>
    /// <param name="component">The component to deactivate.</param>
    public void DeactivateComponent(IComponent component)
    {
        _logger.LogDebug(
            "Deactivating component. ComponentType={ComponentType}",
            component.GetType().Name
        );

        switch (component)
        {
            case IGraphicsComponent graphicsComponent:
                graphics.Deactivate(graphicsComponent);
                return;
            case IAudioComponent audioComponent:
                audio.Deactivate(audioComponent);
                return;
            case IPhysicsComponent physicsComponent:
                physics.Deactivate(physicsComponent);
                return;
        }
    }

    /// <summary>
    /// Activates every component belonging to a game object.
    /// </summary>
    /// <param name="gameObject">The game object to activate.</param>
    public void ActivateGameObject(IGameObject gameObject)
    {
        _logger.LogDebug(
            "Activating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        foreach (var component in gameObject.Components)
        {
            ActivateComponent(component);
        }
    }

    /// <summary>
    /// Deactivates every component belonging to a game object.
    /// </summary>
    /// <param name="gameObject">The game object to deactivate.</param>
    public void DeactivateGameObject(IGameObject gameObject)
    {
        _logger.LogDebug(
            "Deactivating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        foreach (var component in gameObject.Components)
        {
            DeactivateComponent(component);
        }
    }
}
