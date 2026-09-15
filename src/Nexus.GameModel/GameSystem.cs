namespace Nexus.GameModel;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem(IGraphicsSystem graphics, IPhysicsSystem physics, IAudioSystem audio)
    : IGameSystem
{
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
        }
    }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    public void Initialize()
    {
        if (InitialSceneId == GameObjectId.Invalid)
            throw new InvalidOperationException("Initial Scene is not defined.");

        CurrentScene = new Scene();

        var background = CurrentScene.AddComponent<UniformColorMeshRenderer>();

        background.Color = Colors.CornflowerBlue;
        background.Geometry = new VertexGeometryResourceDescription(
            "Background",
            [new(-1f, -1f, 0f), new(3f, -1f, 0f), new(-1f, 3f, 0f)]
        );

        CurrentScene.Activate();
    }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime)
    {
        CurrentScene?.Update(deltaTime);
    }

    public void ActivateComponent(IComponent component)
    {
        if (graphics.CanActivate(component))
        {
            graphics.Activate(component);
        }

        if (physics.CanActivate(component))
        {
            physics.Activate(component);
        }

        if (audio.CanActivate(component))
        {
            audio.Activate(component);
        }
    }

    public void DeactivateComponent(IComponent component)
    {
        graphics.Deactivate(component);
        physics.Deactivate(component);
        audio.Deactivate(component);
    }

    public void ActivateGameObject(IGameObject gameObject)
    {
        foreach (var component in gameObject.Components)
        {
            ActivateComponent(component);
        }
    }

    public void DeactivateGameObject(IGameObject gameObject)
    {
        foreach (var component in gameObject.Components)
        {
            DeactivateComponent(component);
        }
    }
}
