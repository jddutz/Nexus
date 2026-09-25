namespace Nexus.Game;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem(
    IEventHub eventHub,
    IContentProvider<Texture> textureProvider,
    IContentManifest contentManifest,
    IFontBuilder fontBuilder,
    ILogger<GameSystem> logger
) : IGameSystem, IGameModel
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly IContentManifest _contentManifest = contentManifest;
    private readonly IFontBuilder _fontBuilder = fontBuilder;
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
            "Initializing game system. InitialSceneType={InitialSceneType}",
            InitialScene.GetType().Name
        );

        InitialScene.SetGameModel(this);
        CurrentScene = InitialScene;
        CurrentScene.CreateChild<View>();

        // Every renderable pipeline expects a bound set-0 camera descriptor. OrthoCamera (not
        // StaticCamera) because its symmetric [-1,1] extent matches this demo's NDC-sized world
        // coordinates 1:1 - StaticCamera's top-left-origin [0,width] extent would scale and
        // offset everything incorrectly.
        var camera = CurrentScene.CreateChild<GameObject>().AddComponent<OrthoCamera>();
        camera.Width = 2f;
        camera.Height = 2f;

        var textComponent = new TextComponent(CreateRobotoTextStyle()) { Text = "Hello Nexus" };
        CurrentScene.CreateChild<GameObject2D>().AddComponent(textComponent);

        _logger.LogTrace("Activating scene...");
        CurrentScene.Activate();
        _logger.LogTrace("Scene activation complete.");
        _logger.LogInformation("Game system initialized and initial scene activated.");
    }

    /// <summary>
    /// Builds the default Roboto text style from the font registered as <c>ui.default</c>.
    /// </summary>
    /// <returns>The generated style at size 18 with an off-white color.</returns>
    private ITextStyle CreateRobotoTextStyle()
    {
        var fontId = (ContentId)"ui.default";
        var fontPath = Path.Combine(
            _contentManifest.ContentLibraryPath,
            _contentManifest.Fonts.GetContentFilePath(fontId)
        );
        var codepoints = new FontGlyphRepertoire().GetCodepoints();
        var font = _fontBuilder.Build(fontPath, codepoints, new FontGenerationSettings());
        var atlas = font.Atlas;
        var colors = new Color[checked(atlas.Width * atlas.Height)];

        for (var index = 0; index < colors.Length; index++)
        {
            var sourceOffset = index * 3;
            colors[index] = new Color(
                atlas.Pixels[sourceOffset],
                atlas.Pixels[sourceOffset + 1],
                atlas.Pixels[sourceOffset + 2]
            );
        }

        var texture = new Texture(
            (ContentId)"Roboto",
            checked((uint)atlas.Width),
            checked((uint)atlas.Height),
            colors
        );

        return new TextStyle(font, texture, 18, Colors.WhiteSmoke);
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
        _logger.LogTrace(
            "Deactivating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        _eventHub.Publish(new GameObjectDeactivatedEvent(gameObject));
    }
}
