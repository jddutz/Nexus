using Nexus.Core;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;

namespace Tests;

/// <summary>
/// Tests game object component ownership and game-model lookup behavior.
/// </summary>
public class GameObjectTests
{
    /// <summary>
    /// Verifies that a scene has its own identity and cannot be attached to a game object.
    /// </summary>
    [Fact]
    public void Scene_isTopLevelEntityWithSceneId()
    {
        var scene = new Scene(42);

        Assert.Equal(new SceneId(42), scene.Id);
        Assert.IsType<StaticCamera>(scene.DefaultCamera);
        Assert.IsType<GameObject2D>(scene.DefaultView);
        Assert.Contains(scene.DefaultView, scene.Children);
        Assert.Contains(
            scene.DefaultView.Components,
            component => ReferenceEquals(component, scene.DefaultCamera)
        );
        Assert.Contains(scene.DefaultView.Components, component => component is ViewComponent);
        Assert.IsNotAssignableFrom<IGameObject>(scene);
        var view = new View();
        Assert.Equal(nameof(ViewComponent), view.ViewComponent.Name);
        Assert.Equal(RenderPasses.Main, view.ViewComponent.RenderPassMask);
        Assert.Contains(view.ViewComponent, view.Components);
    }

    /// <summary>
    /// Verifies that activating a scene raises lifecycle notifications for its default view and camera.
    /// </summary>
    [Fact]
    public void Scene_activatesDefaultViewAndCameraThroughLifecycleEvents()
    {
        var scene = new Scene();
        var addedGameObjects = new List<IGameObject>();
        var addedComponents = new List<IComponent>();

        scene.GameObjectAdded += addedGameObjects.Add;
        scene.ComponentAdded += addedComponents.Add;

        scene.Activate();

        Assert.Contains(scene.DefaultView, addedGameObjects);
        Assert.Contains(
            addedComponents,
            component => ReferenceEquals(component, scene.DefaultCamera)
        );
        Assert.Contains(
            scene.DefaultView.Components,
            component => component is ViewComponent && addedComponents.Contains(component)
        );
    }

    /// <summary>
    /// Verifies that scenes forward descendant component and game-object lifecycle notifications.
    /// </summary>
    [Fact]
    public void Scene_forwardsDescendantLifecycleNotifications()
    {
        var scene = new Scene();
        var parent = scene.CreateChild<GameObject>();
        var child = parent.CreateChild<GameObject>();
        var component = new TestComponent();
        var addedGameObjects = new List<IGameObject>();
        var removedGameObjects = new List<IGameObject>();
        var addedComponents = new List<IComponent>();
        var removedComponents = new List<IComponent>();

        scene.GameObjectAdded += addedGameObjects.Add;
        scene.GameObjectRemoved += removedGameObjects.Add;
        scene.ComponentAdded += addedComponents.Add;
        scene.ComponentRemoved += removedComponents.Add;

        child.AddComponent(component);
        scene.Activate();

        Assert.Contains(parent, addedGameObjects);
        Assert.Contains(component, addedComponents);

        parent.RemoveChild(child);

        Assert.Contains(child, removedGameObjects);
        Assert.Contains(component, removedComponents);
    }

    /// <summary>
    /// Verifies that components can resolve their owner through the assigned game model.
    /// </summary>
    [Fact]
    public void AddComponent_assignsOwnerAndGameModel()
    {
        var gameModel = new TestGameModel();
        var gameObject = new GameObject(1);
        var component = new TestComponent();

        gameObject.SetGameModel(gameModel);
        gameObject.AddComponent(component);

        var componentGameModel = Assert.IsAssignableFrom<IGameModel>(component.GameModel);

        Assert.Equal(gameObject.Id, component.GameObjectId);
        Assert.Same(gameModel, componentGameModel);
        Assert.Same(gameObject, componentGameModel.GetGameObject(component.GameObjectId));
    }

    /// <summary>
    /// Verifies that attaching a component to a new game object detaches it from its prior owner.
    /// </summary>
    [Fact]
    public void AddComponent_transfersComponentFromPreviousOwner()
    {
        var gameModel = new TestGameModel();
        var previousOwner = new GameObject(1);
        var nextOwner = new GameObject(2);
        var component = new TestComponent();

        previousOwner.SetGameModel(gameModel);
        nextOwner.SetGameModel(gameModel);
        previousOwner.AddComponent(component);

        nextOwner.AddComponent(component);

        Assert.Empty(previousOwner.Components);
        Assert.Contains(component, nextOwner.Components);
        Assert.Equal(nextOwner.Id, component.GameObjectId);
        Assert.Same(nextOwner, component.GameModel?.GetGameObject(component.GameObjectId));
    }

    /// <summary>
    /// Provides an in-memory game model for component ownership tests.
    /// </summary>
    private sealed class TestGameModel : IGameModel
    {
        private readonly Dictionary<GameObjectId, IGameObject> _gameObjects = [];

        /// <inheritdoc/>
        public IGameObject? GetGameObject(GameObjectId gameObjectId) =>
            _gameObjects.GetValueOrDefault(gameObjectId);

        /// <inheritdoc/>
        public void RegisterGameObject(IGameObject gameObject) =>
            _gameObjects[gameObject.Id] = gameObject;

        /// <inheritdoc/>
        public void UnregisterGameObject(IGameObject gameObject) =>
            _gameObjects.Remove(gameObject.Id);
    }

    /// <summary>
    /// Provides a concrete component for ownership tests.
    /// </summary>
    private sealed class TestComponent : Component
    {
        /// <inheritdoc />
        public override string DisplayName => "Test Component";
    }
}
