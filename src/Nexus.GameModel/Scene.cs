namespace Nexus.GameModel;

public class Scene : GameObject, IScene
{
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
    {
        ChildAdded += OnChildAdded;
        ChildRemoved += OnChildRemoved;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with the specified identifier.
    /// </summary>
    /// <param name="sceneId">The identifier for the scene.</param>
    public Scene(uint sceneId)
        : base(sceneId)
    {
        ChildAdded += OnChildAdded;
        ChildRemoved += OnChildRemoved;
    }

    /// <summary>
    /// Raises the scene notification for an added game object.
    /// </summary>
    /// <param name="gameObject">The added game object.</param>
    private void OnChildAdded(IGameObject gameObject)
    {
        GameObjectAdded?.Invoke(gameObject);
    }

    /// <summary>
    /// Raises the scene notification for a removed game object.
    /// </summary>
    /// <param name="gameObject">The removed game object.</param>
    private void OnChildRemoved(IGameObject gameObject)
    {
        GameObjectRemoved?.Invoke(gameObject);
    }
}
