namespace Nexus.Game;

/// <summary>
/// Provides a top-level container for game objects.
/// </summary>
public class Scene : IScene
{
    private readonly List<IGameObject> _children = [];
    private IGameModel? _gameModel;
    private IInputSystem? _inputSystem;
    private InputMap? _inputMap;
    private bool _isActive;

    /// <summary>
    /// Gets the unique identifier for this scene.
    /// </summary>
    public SceneId Id { get; }

    /// <summary>
    /// Gets the current set of render layers managed by the graphics system.
    /// </summary>
    RenderLayerCollection Layers { get; }

    /// <summary>
    /// Gets the game objects directly contained by this scene.
    /// </summary>
    public IReadOnlyList<IGameObject> Children => _children.AsReadOnly();

    /// <summary>
    /// Gets the game model that owns the game objects in this scene.
    /// </summary>
    public IGameModel? GameModel => _gameModel;

    /// <summary>
    /// Gets a value indicating whether this scene is active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets or sets the input map selected while this scene is active.
    /// </summary>
    public InputMap? InputMap
    {
        get => _inputMap;
        set
        {
            if (ReferenceEquals(_inputMap, value))
                return;

            var previousMap = _inputMap;
            _inputMap = value;

            if (
                _isActive
                && _inputSystem is not null
                && ReferenceEquals(_inputSystem.CurrentMap, previousMap)
            )
                _inputSystem.CurrentMap = value;
        }
    }

    /// <summary>
    /// Occurs when a component is added to a game object in this scene.
    /// </summary>
    public event Action<IComponent>? ComponentAdded;

    /// <summary>
    /// Occurs when a component is removed from a game object in this scene.
    /// </summary>
    public event Action<IComponent>? ComponentRemoved;

    /// <summary>
    /// Occurs when a game object is added to this scene or one of its descendants.
    /// </summary>
    public event Action<IGameObject>? GameObjectAdded;

    /// <summary>
    /// Occurs when a game object is removed from this scene or one of its descendants.
    /// </summary>
    public event Action<IGameObject>? GameObjectRemoved;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with a generated identifier.
    /// </summary>
    public Scene()
        : this(SceneId.New(), null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified input system.
    /// </summary>
    /// <param name="inputSystem">The input system used to select this scene's input map.</param>
    public Scene(IInputSystem inputSystem)
        : this(SceneId.New(), inputSystem) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier.
    /// </summary>
    /// <param name="sceneId">The identifier for the scene.</param>
    public Scene(SceneId sceneId)
        : this(sceneId, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier and input system.
    /// </summary>
    /// <param name="sceneId">The identifier for the scene.</param>
    /// <param name="inputSystem">The input system used to select this scene's input map.</param>
    public Scene(SceneId sceneId, IInputSystem? inputSystem)
    {
        Id = sceneId;
        _inputSystem = inputSystem;

        Layers = new RenderLayerCollection();
        Layers.Create("GUI", RenderPasses.Main);

        var defaultCamera = new StaticCamera();
        var viewComponent = new ViewComponent() { Camera = defaultCamera, LayerMask = 1 };
        var defaultView = new GameObject2D([defaultCamera, viewComponent]);

        AddChild(defaultView);
    }

    /// <summary>
    /// Associates this scene and its game objects with a game model.
    /// </summary>
    /// <param name="gameModel">The game model that owns this scene's game objects.</param>
    public void SetGameModel(IGameModel gameModel)
    {
        ArgumentNullException.ThrowIfNull(gameModel);
        if (_gameModel == gameModel)
            return;

        _gameModel = gameModel;
        foreach (var child in _children.OfType<GameObject>())
            child.SetGameModel(gameModel);
    }

    /// <summary>
    /// Associates this scene with the input system that selects its map during activation.
    /// </summary>
    /// <param name="inputSystem">The input system to associate, or <see langword="null"/>.</param>
    internal void SetInputSystem(IInputSystem? inputSystem)
    {
        if (ReferenceEquals(_inputSystem, inputSystem))
            return;

        var wasSelected =
            _isActive
            && _inputSystem is not null
            && ReferenceEquals(_inputSystem.CurrentMap, _inputMap);
        if (wasSelected)
            _inputSystem!.CurrentMap = null;

        _inputSystem = inputSystem;
        if (_isActive && _inputSystem is not null)
            _inputSystem.CurrentMap = _inputMap;
    }

    /// <summary>
    /// Adds a top-level game object to this scene.
    /// </summary>
    /// <param name="child">The game object to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the game object already belongs to a parent.</exception>
    public void AddChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
            throw new InvalidOperationException("GameObject already belongs to a parent.");

        _children.Add(child);
        ListenToChild(child);
        if (child is GameObject gameObject && _gameModel is not null)
            gameObject.SetGameModel(_gameModel);

        if (_isActive)
        {
            GameObjectAdded?.Invoke(child);
            child.Activate();
        }
    }

    /// <summary>
    /// Removes a top-level game object from this scene.
    /// </summary>
    /// <param name="child">The game object to remove.</param>
    /// <returns><see langword="true"/> when the game object was removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveChild(IGameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child))
            return false;

        if (_isActive)
        {
            child.Deactivate();
            GameObjectRemoved?.Invoke(child);
        }

        StopListeningToChild(child);
        return true;
    }

    /// <summary>
    /// Creates and adds a top-level game object of the specified type.
    /// </summary>
    /// <typeparam name="TChild">The type of game object to create.</typeparam>
    /// <returns>The added game object.</returns>
    public TChild CreateChild<TChild>()
        where TChild : IGameObject, new()
    {
        var child = new TChild();
        AddChild(child);
        return child;
    }

    /// <summary>
    /// Activates this scene and its game objects.
    /// </summary>
    public void Activate()
    {
        if (_isActive)
            return;

        _isActive = true;
        if (_inputSystem is not null)
            _inputSystem.CurrentMap = _inputMap;

        foreach (var child in _children)
        {
            GameObjectAdded?.Invoke(child);
            child.Activate();
        }
    }

    /// <summary>
    /// Updates the game objects in this scene.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime)
    {
        foreach (var child in _children)
            child.Update(deltaTime);
    }

    /// <summary>
    /// Deactivates this scene and its game objects.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
            return;

        if (_inputSystem is not null && ReferenceEquals(_inputSystem.CurrentMap, _inputMap))
            _inputSystem.CurrentMap = null;

        foreach (var child in _children)
        {
            child.Deactivate();
            GameObjectRemoved?.Invoke(child);
        }

        _isActive = false;
    }

    /// <summary>
    /// Subscribes to events raised by a game object in this scene.
    /// </summary>
    /// <param name="child">The game object to observe.</param>
    private void ListenToChild(IGameObject child)
    {
        child.ComponentAdded += OnChildComponentAdded;
        child.ComponentRemoved += OnChildComponentRemoved;
        child.ChildAdded += OnChildAdded;
        child.ChildRemoved += OnChildRemoved;
    }

    /// <summary>
    /// Removes subscriptions to events raised by a removed game object.
    /// </summary>
    /// <param name="child">The game object to stop observing.</param>
    private void StopListeningToChild(IGameObject child)
    {
        child.ComponentAdded -= OnChildComponentAdded;
        child.ComponentRemoved -= OnChildComponentRemoved;
        child.ChildAdded -= OnChildAdded;
        child.ChildRemoved -= OnChildRemoved;
    }

    /// <summary>
    /// Propagates a component-added notification from a game object in this scene.
    /// </summary>
    /// <param name="component">The added component.</param>
    private void OnChildComponentAdded(IComponent component)
    {
        if (_isActive)
            ComponentAdded?.Invoke(component);
    }

    /// <summary>
    /// Propagates a component-removed notification from a game object in this scene.
    /// </summary>
    /// <param name="component">The removed component.</param>
    private void OnChildComponentRemoved(IComponent component)
    {
        if (_isActive)
            ComponentRemoved?.Invoke(component);
    }

    /// <summary>
    /// Propagates a game-object-added notification from a descendant.
    /// </summary>
    /// <param name="gameObject">The added game object.</param>
    private void OnChildAdded(IGameObject gameObject)
    {
        if (_isActive)
            GameObjectAdded?.Invoke(gameObject);
    }

    /// <summary>
    /// Propagates a game-object-removed notification from a descendant.
    /// </summary>
    /// <param name="gameObject">The removed game object.</param>
    private void OnChildRemoved(IGameObject gameObject)
    {
        if (_isActive)
            GameObjectRemoved?.Invoke(gameObject);
    }
}
