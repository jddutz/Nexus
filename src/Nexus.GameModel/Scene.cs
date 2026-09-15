namespace Nexus.GameModel;

public class Scene : GameObject, IScene
{
    public event Action<IComponent>? ComponentAdded;
    public event Action<IComponent>? ComponentRemoved;

    public event Action<IGameObject>? GameObjectAdded;
    public event Action<IGameObject>? GameObjectRemoved;

    public override void OnComponentAdded(IComponent component)
    {
        ComponentAdded?.Invoke(component);
    }

    public override void OnComponentRemoved(IComponent component)
    {
        ComponentRemoved?.Invoke(component);
    }

    public override void OnGameObjectAdded(IGameObject gameObject)
    {
        GameObjectAdded?.Invoke(gameObject);
    }

    public override void OnGameObjectRemoved(IGameObject gameObject)
    {
        GameObjectRemoved?.Invoke(gameObject);
    }

    public Scene(ILogger<GameObject>? logger = null)
        : base(logger) { }

    public Scene(uint sceneId, ILogger<GameObject>? logger = null)
        : base(sceneId, logger) { }
}
