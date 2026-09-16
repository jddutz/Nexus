using Nexus.Graphics.Geometry;

namespace Nexus.GameModel;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameModelSystem(
    IGraphicsSystem graphics,
    IPhysicsSystem physics,
    IAudioSystem audio,
    IInputSystem input,
    ILogger<GameModelSystem> logger
) : IGameSystem, IGameModel
{
    private readonly ILogger<GameModelSystem> _logger = logger;
    private readonly Dictionary<GameObjectId, IGameObject> _gameObjects = [];

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
    }

    /// <inheritdoc/>
    public void UnregisterGameObject(IGameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        _gameObjects.Remove(gameObject.Id);
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

        CurrentScene = new Scene();
        CurrentScene.SetGameModel(this);

        // Every renderable pipeline expects a bound set-0 camera descriptor.
        CurrentScene.CreateChild<GameObject>().AddComponent<StaticCamera>();

        var columns = 16;
        var rows = 9;

        var cellWidth = 2.0f / columns;
        var cellHeight = 2.0f / rows;

        var rng = new Random();

        var geometry = new UniformColorVertexGeometry(
            "Rect",
            [new(-0.5f, -0.5f, 0f), new(0.5f, -0.5f, 0f), new(-0.5f, 0.5f, 0f), new(0.5f, 0.5f, 0f)]
        );

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var component = CurrentScene
                    .CreateChild<GameObject>()
                    .AddComponent<UniformColorMeshRenderer>();

                var c = 0.01f + (float)rng.NextDouble() * 0.02f;
                component.Color = new Color(c, c, c, 1.0f);

                var scale = 1.0f + (float)rng.NextDouble() * 0.4f;

                var width = cellWidth * scale;
                var height = cellHeight * scale;

                var centerX = -1.0f + (x + 0.5f) * cellWidth;
                var centerY = -1.0f + (y + 0.5f) * cellHeight;

                component.TransformationMatrix =
                    Matrix4X4.CreateScale(width, height, 1.0f)
                    * Matrix4X4.CreateTranslation(centerX, centerY, rng.Next(4));

                component.Geometry = geometry;
            }
        }

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
            case IInputComponent inputComponent:
                input.Deactivate(inputComponent);
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
