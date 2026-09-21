namespace Nexus.GameModel;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem(
    IEventHub eventHub,
    IContentProvider<Texture> textureProvider,
    ILogger<GameSystem> logger
) : IGameSystem, IGameModel
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly ILogger<GameSystem> _logger = logger;
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

        // Every renderable pipeline expects a bound set-0 camera descriptor. OrthoCamera (not
        // StaticCamera) because its symmetric [-1,1] extent matches this demo's NDC-sized world
        // coordinates 1:1 - StaticCamera's top-left-origin [0,width] extent would scale and
        // offset everything incorrectly.
        var camera = CurrentScene.CreateChild<GameObject>().AddComponent<OrthoCamera>();
        camera.Width = 2f;
        camera.Height = 2f;

        var columns = 16;
        var rows = 9;

        var cellWidth = 2.0f / columns;
        var cellHeight = 2.0f / rows;

        var rng = new Random();

        var atlasColumns = 14;
        var atlasRows = 13;

        var regionWidth = 1.0f / atlasColumns;
        var regionHeight = 1.0f / atlasRows;

        var atlasTexture = textureProvider.Get("button_atlas");

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var component = new TexturedQuadRenderer(centered: true);
                CurrentScene.CreateChild<GameObject>().AddComponent(component);

                component.Texture = atlasTexture;

                var atlasIndex = (x * rows + y) % (atlasColumns * atlasRows);
                var atlasX = atlasIndex % atlasColumns;
                var atlasY = atlasIndex / atlasColumns;

                component.TextureRegion = new(
                    atlasX * regionWidth,
                    atlasY * regionHeight,
                    regionWidth,
                    regionHeight
                );
                /*
                var idx = (x + y) % 4;
                component.TextureRegion = idx switch
                {
                    0 => new(0.0f, 0.0f, 0.5f, 0.5f),
                    1 => new(0.5f, 0.0f, 0.5f, 0.5f),
                    2 => new(0.0f, 0.5f, 0.5f, 0.5f),
                    _ => new(0.5f, 0.5f, 0.5f, 0.5f),
                };

                // component.Color = Colors.RandomGray(rng, 0.01f, 0.02f);

                var scale = 1.0f + (float)rng.NextDouble() * 0.4f;

                var width = cellWidth * scale;
                var height = cellHeight * scale;

                var centerX = -1.0f + (x + 0.5f) * cellWidth;
                var centerY = -1.0f + (y + 0.5f) * cellHeight;
                */

                var width = cellWidth * 0.9f;
                var height = cellHeight * 0.9f;

                var centerX = -1.0f + (x + 0.5f) * cellWidth;
                var centerY = -1.0f + (y + 0.5f) * cellHeight;

                component.TransformationMatrix =
                    Matrix4X4.CreateScale(width, height, 1.0f)
                    * Matrix4X4.CreateTranslation(centerX, centerY, rng.Next(4));
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

    /// <summary>Publishes a component activation event.</summary>
    /// <param name="component">The component to activate.</param>
    public void ActivateComponent(IComponent component)
    {
        _logger.LogDebug(
            "Activating component. ComponentType={ComponentType}",
            component.GetType().Name
        );

        _eventHub.Publish(new ComponentActivatedEvent(component));
    }

    /// <summary>Publishes a component deactivation event.</summary>
    /// <param name="component">The component to deactivate.</param>
    public void DeactivateComponent(IComponent component)
    {
        _logger.LogDebug(
            "Deactivating component. ComponentType={ComponentType}",
            component.GetType().Name
        );

        _eventHub.Publish(new ComponentDeactivatedEvent(component));
    }

    /// <summary>Publishes activation events for a game object and its components.</summary>
    /// <param name="gameObject">The game object to activate.</param>
    public void ActivateGameObject(IGameObject gameObject)
    {
        _logger.LogDebug(
            "Activating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        _eventHub.Publish(new GameObjectActivatedEvent(gameObject));

        foreach (var component in gameObject.Components)
            ActivateComponent(component);
    }

    /// <summary>Publishes deactivation events for a game object and its components.</summary>
    /// <param name="gameObject">The game object to deactivate.</param>
    public void DeactivateGameObject(IGameObject gameObject)
    {
        _logger.LogDebug(
            "Deactivating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        foreach (var component in gameObject.Components)
            DeactivateComponent(component);

        _eventHub.Publish(new GameObjectDeactivatedEvent(gameObject));
    }
}
