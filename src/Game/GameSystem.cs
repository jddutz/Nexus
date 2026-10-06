using Nexus.Core.Performance;
using Nexus.Graphics.Events;

namespace Nexus.Game;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
/// <param name="eventHub">The event hub used to register handlers and publish lifecycle events.</param>
/// <param name="logger">The logger used for game-system diagnostics.</param>
/// <param name="windowService">The service used to synchronize the current scene camera with the main window, or <see langword="null"/> in a headless runtime.</param>
/// <param name="telemetry">The optional performance telemetry sink.</param>
public partial class GameSystem(
    IEventHub eventHub,
    ILogger<GameSystem> logger,
    IWindowService? windowService = null,
    IPerformanceTelemetry? telemetry = null
) : IGameSystem, IObservable
{
    private readonly IEventHub _eventHub = eventHub;
    private readonly ILogger<GameSystem> _logger = logger;
    private readonly IWindowService? _windowService = windowService;
    private readonly HashSet<ISceneNode> _subscribedSceneNodes = [];
    private readonly HashSet<IGameObject> _publishedGameObjectActivations = new(
        ReferenceEqualityComparer.Instance
    );
    private readonly HashSet<IGameObject> _activatingGameObjects = new(
        ReferenceEqualityComparer.Instance
    );
    private readonly HashSet<object> _removedDuringTraversal = new(
        ReferenceEqualityComparer.Instance
    );
    private bool _isTraversing;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <summary>
    /// Represents an entity and its expected ownership in a lifecycle traversal snapshot.
    /// </summary>
    private sealed class LifecycleEntry
    {
        /// <summary>
        /// Initializes a lifecycle entry with its entity and captured ownership.
        /// </summary>
        /// <param name="entity">The managed entity represented by the entry, if any.</param>
        /// <param name="node">The scene node represented by the entry, if any.</param>
        /// <param name="parentNode">The expected parent scene node.</param>
        /// <param name="componentOwner">The expected component owner.</param>
        /// <param name="parentIndex">The parent entry index, or -1 for the scene root.</param>
        public LifecycleEntry(
            IManagedEntity? entity,
            ISceneNode? node,
            ISceneNode? parentNode,
            IGameObject? componentOwner,
            int parentIndex
        )
        {
            Entity = entity;
            Node = node;
            ParentNode = parentNode;
            ComponentOwner = componentOwner;
            ParentIndex = parentIndex;
        }

        /// <summary>Gets the managed entity represented by the entry.</summary>
        public IManagedEntity? Entity { get; }

        /// <summary>Gets the scene node represented by the entry.</summary>
        public ISceneNode? Node { get; }

        /// <summary>Gets the expected parent scene node.</summary>
        public ISceneNode? ParentNode { get; }

        /// <summary>Gets the expected owner when the entry represents a component.</summary>
        public IGameObject? ComponentOwner { get; }

        /// <summary>Gets the parent lifecycle entry index, or -1 for the scene root.</summary>
        public int ParentIndex { get; }
    }

    [Observable(Public = false)]
    private IScene? _currentScene = null;

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    public void Initialize()
    {
        using var timing = new LoadPerformanceScope(
            telemetry,
            "startup.system.initialize",
            "gameSystem"
        );
        var initialScene = CurrentScene;
        if (initialScene is null)
            throw new InvalidOperationException(
                "No current scene is loaded. Load a scene before initializing the game system."
            );

        _logger.LogInformation(
            "Initializing game system. StartSceneType={StartSceneType}",
            initialScene.GetType().Name
        );

        _eventHub.Register(this);

        _logger.LogTrace(
            "Initial scene selected; entity initialization and activation are deferred until lifecycle traversal."
        );
        _logger.LogInformation("Game system initialized and initial scene selected.");
    }

    /// <summary>Loads the supplied scene, unloading any scene that is currently active.</summary>
    /// <param name="scene">The scene to make current.</param>
    public void LoadScene(IScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        CurrentScene = scene;
    }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime)
    {
        if (CurrentScene is not { } currentScene)
            return;

        RunLifecycleTraversal(currentScene, deltaTime, updateEntities: true);
    }

    /// <summary>Publishes a component activation event.</summary>
    /// <param name="component">The component to activate.</param>
    public void ActivateComponent(IComponent component)
    {
        if (!TryActivate(component))
            return;

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
        SubscribeSceneNode(gameObject);
        if (!_activatingGameObjects.Add(gameObject))
            return;

        try
        {
            if (!TryActivate(gameObject))
                return;

            if (gameObject.Parent is IGameObject parent && _activatingGameObjects.Contains(parent))
                return;

            PublishActivatedSubtree(gameObject);
        }
        finally
        {
            _activatingGameObjects.Remove(gameObject);
        }
    }

    /// <summary>Publishes a game-object deactivation event.</summary>
    /// <param name="gameObject">The game object to deactivate.</param>
    public void DeactivateGameObject(IGameObject gameObject)
    {
        var activationIsPending = _activatingGameObjects.Contains(gameObject);
        _publishedGameObjectActivations.Remove(gameObject);
        UnregisterEventHandlers(gameObject);
        if (CurrentScene is not { } currentScene || !ReferenceEquals(gameObject.Root, currentScene))
            UnsubscribeSceneNode(gameObject);

        if (!gameObject.IsActivated)
            return;

        gameObject.Deactivate();

        _logger.LogTrace(
            "Deactivating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        if (!activationIsPending)
            _eventHub.Publish(new GameObjectDeactivatedEvent(gameObject));
    }

    /// <summary>
    /// Attempts to initialize and activate an entity when it is not already active.
    /// </summary>
    /// <param name="entity">The entity to activate.</param>
    /// <returns><see langword="true"/> if the entity became activated; otherwise, <see langword="false"/>.</returns>
    private static bool TryActivate(IManagedEntity entity)
    {
        if (!entity.IsInitialized)
            entity.Initialize();

        if (entity.IsActivated || !entity.CanActivate())
            return false;

        entity.Activate();
        return entity.IsActivated;
    }

    /// <summary>
    /// Registers and publishes activation events for an already activated object subtree.
    /// </summary>
    /// <param name="gameObject">The root of the activated subtree.</param>
    private void PublishActivatedSubtree(IGameObject gameObject)
    {
        if (!_publishedGameObjectActivations.Add(gameObject))
            return;

        SubscribeSceneNode(gameObject);
        RegisterEventHandlers(gameObject);

        _logger.LogTrace(
            "Activating game object. GameObjectType={GameObjectType}, ComponentCount={ComponentCount}",
            gameObject.GetType().Name,
            gameObject.Components.Count()
        );

        _eventHub.Publish(new GameObjectActivatedEvent(gameObject));

        foreach (var component in gameObject.Components)
        {
            _eventHub.Register(component);
            _logger.LogTrace(
                "Activating component. ComponentType={ComponentType}",
                component.GetType().Name
            );
            _eventHub.Publish(new ComponentActivatedEvent(component));
        }

        foreach (var child in gameObject.Children.OfType<IGameObject>())
            if (child.IsActivated)
                PublishActivatedSubtree(child);
    }

    /// <summary>
    /// Builds and processes one stable, parent-first lifecycle snapshot.
    /// </summary>
    /// <param name="scene">The scene whose hierarchy is traversed.</param>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    /// <param name="updateEntities">Whether activated entities should receive an update.</param>
    private void RunLifecycleTraversal(IScene scene, double deltaTime, bool updateEntities)
    {
        if (_isTraversing)
            return;

        var entries = new List<LifecycleEntry> { new(scene, scene, null, null, -1) };

        foreach (var child in scene.Children.ToArray())
            AppendLifecycleEntries(child, scene, 0, entries);

        _removedDuringTraversal.Clear();
        _isTraversing = true;

        try
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.Entity is not { } entity || !IsInCurrentHierarchy(index, entries))
                    continue;

                var wasInitialized = entity.IsInitialized;
                if (!wasInitialized)
                    entity.Initialize();

                if (!IsInCurrentHierarchy(index, entries))
                    continue;

                if (!wasInitialized && entity is Scene initializedScene)
                    SynchronizeSceneCameraWithMainWindow(initializedScene);

                if (!entity.IsActivated && AreParentsActivated(entry, entries))
                {
                    if (entity is IComponent component)
                        ActivateComponent(component);
                    else if (entity is IGameObject gameObject)
                        ActivateGameObject(gameObject);
                    else
                        TryActivate(entity);
                }

                if (
                    !updateEntities
                    || !entity.IsActivated
                    || !AreParentsActivated(entry, entries)
                    || !IsInCurrentHierarchy(index, entries)
                )
                    continue;

                entity.Update(deltaTime);
            }
        }
        finally
        {
            _isTraversing = false;
            _removedDuringTraversal.Clear();
        }
    }

    /// <summary>
    /// Captures a scene-node subtree and its components in traversal order.
    /// </summary>
    /// <param name="node">The node to capture.</param>
    /// <param name="parentNode">The node that owns <paramref name="node"/>.</param>
    /// <param name="parentIndex">The lifecycle entry index of the managed parent.</param>
    /// <param name="entries">The traversal entries being built.</param>
    private static void AppendLifecycleEntries(
        ISceneNode node,
        ISceneNode parentNode,
        int parentIndex,
        List<LifecycleEntry> entries
    )
    {
        var nodeIndex = entries.Count;
        var gameObject = node as IGameObject;
        entries.Add(new LifecycleEntry(gameObject, node, parentNode, null, parentIndex));

        if (gameObject is not null)
        {
            foreach (var component in gameObject.Components.ToArray())
                entries.Add(new LifecycleEntry(component, null, null, gameObject, nodeIndex));
        }

        foreach (var child in node.Children.ToArray())
            AppendLifecycleEntries(child, node, nodeIndex, entries);
    }

    /// <summary>
    /// Determines whether a snapshot entry and all its ancestors remain in the current scene.
    /// </summary>
    /// <param name="index">The lifecycle entry index.</param>
    /// <param name="entries">The traversal entries.</param>
    /// <returns><see langword="true"/> if the entry remains attached; otherwise, <see langword="false"/>.</returns>
    private bool IsInCurrentHierarchy(int index, IReadOnlyList<LifecycleEntry> entries)
    {
        for (
            var currentIndex = index;
            currentIndex >= 0;
            currentIndex = entries[currentIndex].ParentIndex
        )
        {
            var entry = entries[currentIndex];
            if (
                (entry.Entity is not null && _removedDuringTraversal.Contains(entry.Entity))
                || (entry.Node is not null && _removedDuringTraversal.Contains(entry.Node))
                || !IsDirectlyOwned(entry)
            )
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a lifecycle entry is still owned by its captured parent.
    /// </summary>
    /// <param name="entry">The lifecycle entry to validate.</param>
    /// <returns><see langword="true"/> if the captured ownership still holds; otherwise, <see langword="false"/>.</returns>
    private bool IsDirectlyOwned(LifecycleEntry entry)
    {
        if (entry.Entity is IScene scene)
            return ReferenceEquals(CurrentScene, scene);

        if (entry.Entity is IComponent component)
        {
            return entry.ComponentOwner is { } owner
                && ReferenceEquals(component.Owner, owner)
                && owner.Components.Contains(component);
        }

        return entry.Node is { } node
            && entry.ParentNode is { } parent
            && ReferenceEquals(node.Parent, parent)
            && parent.Children.Contains(node);
    }

    /// <summary>
    /// Determines whether every managed ancestor of an entity is activated.
    /// </summary>
    /// <param name="entry">The lifecycle entry whose ancestors are checked.</param>
    /// <param name="entries">The traversal entries.</param>
    /// <returns><see langword="true"/> if all managed ancestors are activated; otherwise, <see langword="false"/>.</returns>
    private static bool AreParentsActivated(
        LifecycleEntry entry,
        IReadOnlyList<LifecycleEntry> entries
    )
    {
        for (
            var parentIndex = entry.ParentIndex;
            parentIndex >= 0;
            parentIndex = entries[parentIndex].ParentIndex
        )
        {
            if (entries[parentIndex].Entity is { IsActivated: false })
                return false;
        }

        return true;
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
    /// Deactivates the previous scene and tracks the newly selected current scene.
    /// Initialization and activation of the current scene are performed during lifecycle traversal.
    /// </summary>
    /// <param name="previousValue">The scene active before the assignment.</param>
    protected virtual partial void AfterCurrentSceneChanges(IScene? previousValue)
    {
        if (previousValue is not null)
        {
            previousValue.PropertyChanged -= OnCurrentScenePropertyChanged;
            UnsubscribeSceneNode(previousValue);

            if (previousValue is Scene previousInputScene)
            {
                previousInputScene.InputMapChanged -= OnCurrentSceneInputMapChanged;
                previousInputScene.InputMap?.Unregister(_eventHub);
            }

            foreach (var child in previousValue.Children.ToArray().Reverse())
                DeactivateSubtree(child);

            if (previousValue.IsActivated)
                previousValue.Deactivate();

            _eventHub.Publish(new SceneUnloadedEvent(previousValue));
        }

        var currentScene = CurrentScene;
        if (currentScene is not null)
        {
            currentScene.PropertyChanged += OnCurrentScenePropertyChanged;
            SubscribeSceneNode(currentScene);

            if (currentScene is Scene scene)
            {
                scene.InputMapChanged += OnCurrentSceneInputMapChanged;

                if (_windowService is not null)
                {
                    _eventHub.Register(this);
                    if (scene.IsInitialized)
                        SynchronizeSceneCameraWithMainWindow(scene);
                }
            }

            _eventHub.Publish(new SceneLoadedEvent(currentScene));
        }

        _logger.LogInformation(
            "Current scene changed. PreviousSceneType={PreviousSceneType}, CurrentSceneType={CurrentSceneType}",
            previousValue?.GetType().Name ?? "None",
            currentScene?.GetType().Name ?? "None"
        );
    }

    /// <summary>Synchronizes an initialized scene's camera viewport with the main window.</summary>
    /// <param name="scene">The initialized scene whose camera is synchronized.</param>
    private void SynchronizeSceneCameraWithMainWindow(Scene scene)
    {
        if (_windowService is null)
            return;

        var windowSize = _windowService.GetMainWindow().Size;
        scene.MainCamera.SetViewportSize(windowSize.X, windowSize.Y);
    }

    /// <summary>
    /// Updates the active scene's main camera when the main window changes size.
    /// </summary>
    /// <param name="message">The window-resized event.</param>
    public void Handle(WindowResizedEvent message)
    {
        if (
            _windowService is null
            || message.WindowId != _windowService.MainWindowId
            || CurrentScene is not Scene scene
            || !scene.IsInitialized
        )
            return;

        scene.MainCamera.SetViewportSize(message.Size.X, message.Size.Y);
    }

    /// <summary>
    /// Deactivates a scene-node subtree in child-first order.
    /// </summary>
    /// <param name="node">The root node to deactivate.</param>
    private void DeactivateSubtree(ISceneNode node)
    {
        foreach (var child in node.Children.ToArray().Reverse())
            DeactivateSubtree(child);

        if (node is not IGameObject gameObject)
            return;

        foreach (var component in gameObject.Components.ToArray().Reverse())
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

        foreach (var child in node.Children.ToArray())
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

        foreach (var child in node.Children.ToArray())
            UnsubscribeSceneNode(child);
    }

    /// <summary>
    /// Subscribes to and activates a child subtree added to the active scene.
    /// </summary>
    /// <param name="child">The newly added child node.</param>
    private void OnSceneChildAdded(ISceneNode child)
    {
        SubscribeSceneNode(child);

        if (child.Parent is IManagedEntity { IsActivated: true } && child is IGameObject gameObject)
            ActivateGameObject(gameObject);
    }

    /// <summary>
    /// Deactivates and unsubscribes from a child subtree removed from the scene.
    /// </summary>
    /// <param name="child">The removed child node.</param>
    private void OnSceneChildRemoved(ISceneNode child)
    {
        if (_isTraversing)
            _removedDuringTraversal.Add(child);

        DeactivateSubtree(child);
        UnsubscribeSceneNode(child);
    }

    /// <summary>
    /// Activates a component added to a game object in the active scene.
    /// </summary>
    /// <param name="component">The newly added component.</param>
    private void OnSceneComponentAdded(IComponent component)
    {
        _eventHub.Publish(new ComponentAddedEvent(component));
    }

    /// <summary>
    /// Activates a component added to an already active game object.
    /// </summary>
    /// <param name="message">The queued component-added event.</param>
    public void Handle(ComponentAddedEvent message)
    {
        if (message.Component.Owner is { IsActivated: true })
            ActivateComponent(message.Component);
    }

    /// <summary>
    /// Deactivates a component removed from a game object in the active scene.
    /// </summary>
    /// <param name="component">The removed component.</param>
    private void OnSceneComponentRemoved(IComponent component)
    {
        if (_isTraversing)
            _removedDuringTraversal.Add(component);

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
        if (propertyName != nameof(IManagedEntity.IsActivated) || CurrentScene is not Scene scene)
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
        if (CurrentScene is not Scene { IsActivated: true })
            return;

        previousMap?.Unregister(_eventHub);
        currentMap?.Register(_eventHub);
    }
}
