namespace Nexus.Game;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
/// <param name="eventHub">The event hub used to register handlers and publish lifecycle events.</param>
/// <param name="logger">The logger used for game-system diagnostics.</param>
/// <param name="sceneRegistry">The registry used to load the configured initial scene.</param>
/// <param name="gameSettings">The settings bound from the Game configuration section.</param>
public partial class GameSystem(
    IEventHub eventHub,
    ILogger<GameSystem> logger,
    ISceneRegistry sceneRegistry,
    IOptions<GameSettings> gameSettings
) : IGameSystem
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly ILogger<GameSystem> _logger = logger;
    private readonly ISceneRegistry _sceneRegistry = sceneRegistry;
    private readonly HashSet<ISceneNode> _subscribedSceneNodes = [];

    /// <summary>Gets the settings bound to the Game configuration section.</summary>
    public GameSettings Settings { get; } = gameSettings.Value;

    /// <summary>
    /// Gets the configured initial scene after initialization, or <see langword="null"/> beforehand.
    /// </summary>
    public IScene? InitialScene { get; private set; }

    [Observable(PublicSetter = false)]
    private IScene? _currentScene;

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

        _logger.LogTrace("Activating scene...");
        SetCurrentScene(initialScene);
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
    private void ActivateSubtree(ISceneNode node)
    {
        if (node is IGameObject gameObject)
        {
            ActivateGameObject(gameObject);
            if (!gameObject.IsActivated)
                return;

            foreach (var component in gameObject.Components)
                ActivateComponent(component);
        }

        foreach (var child in node.Children)
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
    /// Deactivates the previous scene and activates the newly assigned scene.
    /// </summary>
    /// <param name="previousScene">The scene active before the assignment.</param>
    protected virtual void AfterCurrentSceneChanges(IScene? previousScene)
    {
        if (previousScene is not null)
        {
            previousScene.PropertyChanged -= OnCurrentScenePropertyChanged;
            UnsubscribeSceneNode(previousScene);

            if (previousScene is Scene previousInputScene)
            {
                previousInputScene.InputMapChanged -= OnCurrentSceneInputMapChanged;
                previousInputScene.InputMap?.Unregister(_eventHub);
            }

            if (previousScene.IsActivated)
            {
                foreach (var child in previousScene.Children.Reverse())
                    DeactivateSubtree(child);
                previousScene.Deactivate();
            }

            _eventHub.Publish(new SceneUnloadedEvent(previousScene));
        }

        var currentScene = CurrentScene;
        if (currentScene is not null)
        {
            currentScene.Initialize();
            currentScene.PropertyChanged += OnCurrentScenePropertyChanged;
            SubscribeSceneNode(currentScene);

            if (currentScene is Scene scene)
                scene.InputMapChanged += OnCurrentSceneInputMapChanged;

            _eventHub.Publish(new SceneLoadedEvent(currentScene));

            currentScene.Activate();
            if (currentScene.IsActivated)
            {
                if (currentScene is Scene inputScene)
                    inputScene.InputMap?.Register(_eventHub);

                foreach (var child in currentScene.Children)
                    ActivateSubtree(child);
            }
        }

        _logger.LogInformation(
            "Active scene changed. PreviousSceneType={PreviousSceneType}, CurrentSceneType={CurrentSceneType}",
            previousScene?.GetType().Name ?? "None",
            currentScene?.GetType().Name ?? "None"
        );
    }

    /// <summary>
    /// Deactivates a scene-node subtree in child-first order.
    /// </summary>
    /// <param name="node">The root node to deactivate.</param>
    private void DeactivateSubtree(ISceneNode node)
    {
        foreach (var child in node.Children.Reverse())
            DeactivateSubtree(child);

        if (node is not IGameObject gameObject || !gameObject.IsActivated)
            return;

        foreach (var component in gameObject.Components.Reverse())
            DeactivateComponent(component);

        DeactivateGameObject(gameObject);
    }

    /// <summary>
    /// Subscribes to child and component collection changes in a scene subtree.
    /// </summary>
    /// <param name="node">The root node of the subtree.</param>
    private bool SubscribeSceneNode(ISceneNode node)
    {
        if (!_subscribedSceneNodes.Add(node))
            return false;

        node.Children.ItemAdded += OnSceneChildAdded;
        node.Children.ItemRemoved += OnSceneChildRemoved;
        if (node is IGameObject gameObject)
        {
            gameObject.Components.ItemAdded += OnSceneComponentAdded;
            gameObject.Components.ItemRemoved += OnSceneComponentRemoved;
        }

        foreach (var child in node.Children)
            SubscribeSceneNode(child);

        return true;
    }

    /// <summary>
    /// Unsubscribes from child and component collection changes in a scene subtree.
    /// </summary>
    /// <param name="node">The root node of the subtree.</param>
    private void UnsubscribeSceneNode(ISceneNode node)
    {
        if (!_subscribedSceneNodes.Remove(node))
            return;

        node.Children.ItemAdded -= OnSceneChildAdded;
        node.Children.ItemRemoved -= OnSceneChildRemoved;
        if (node is IGameObject gameObject)
        {
            gameObject.Components.ItemAdded -= OnSceneComponentAdded;
            gameObject.Components.ItemRemoved -= OnSceneComponentRemoved;
        }

        foreach (var child in node.Children)
            UnsubscribeSceneNode(child);
    }

    /// <summary>
    /// Subscribes to and activates a child subtree added to the active scene.
    /// </summary>
    /// <param name="child">The newly added child node.</param>
    private void OnSceneChildAdded(ISceneNode child)
    {
        if (!SubscribeSceneNode(child))
            return;

        if (CurrentScene?.IsActivated == true)
            ActivateSubtree(child);
    }

    /// <summary>
    /// Deactivates and unsubscribes from a child subtree removed from the scene.
    /// </summary>
    /// <param name="child">The removed child node.</param>
    private void OnSceneChildRemoved(ISceneNode child)
    {
        if (CurrentScene?.IsActivated == true)
            DeactivateSubtree(child);
        UnsubscribeSceneNode(child);
    }

    /// <summary>
    /// Activates a component added to a game object in the active scene.
    /// </summary>
    /// <param name="component">The newly added component.</param>
    private void OnSceneComponentAdded(IComponent component)
    {
        if (CurrentScene?.IsActivated == true)
            ActivateComponent(component);
    }

    /// <summary>
    /// Deactivates a component removed from a game object in the active scene.
    /// </summary>
    /// <param name="component">The removed component.</param>
    private void OnSceneComponentRemoved(IComponent component)
    {
        if (CurrentScene?.IsActivated == true)
            DeactivateComponent(component);
    }

    /// <summary>
    /// Unregisters a game object from global event handling.
    /// </summary>
    /// <param name="gameObject">The root game object to unregister.</param>
    private void UnregisterEventHandlers(IGameObject gameObject)
    {
        _eventHub.Unregister(gameObject);
    }

    /// <summary>
    /// Registers or unregisters the current scene's input map when its activation state changes.
    /// </summary>
    /// <param name="propertyName">The name of the changed scene property.</param>
    private void OnCurrentScenePropertyChanged(string propertyName)
    {
        if (propertyName != nameof(IManagedEntity.IsActivated) || _currentScene is not Scene scene)
            return;

        if (scene.IsActivated)
            scene.InputMap?.Register(_eventHub);
        else
            scene.InputMap?.Unregister(_eventHub);
    }

    /// <summary>
    /// Replaces the registered input map when the active scene's map changes.
    /// </summary>
    /// <param name="previousMap">The previously assigned map.</param>
    /// <param name="currentMap">The newly assigned map.</param>
    private void OnCurrentSceneInputMapChanged(InputMap? previousMap, InputMap? currentMap)
    {
        if (_currentScene is not Scene { IsActivated: true })
            return;

        previousMap?.Unregister(_eventHub);
        currentMap?.Register(_eventHub);
    }
}
