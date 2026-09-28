using Nexus.Core;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Silk.NET.Maths;

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
        var defaultView = Assert.IsType<GameObject2D>(Assert.Single(scene.Children));
        var defaultCamera = Assert.IsType<StaticCamera>(
            Assert.Single(defaultView.Components.OfType<StaticCamera>())
        );
        var viewComponent = Assert.Single(defaultView.Components.OfType<ViewComponent>());

        Assert.Equal(new SceneId(42), scene.Id);
        Assert.Contains(defaultView, scene.Children);
        Assert.Contains(
            defaultView.Components,
            component => ReferenceEquals(component, defaultCamera)
        );
        Assert.Same(defaultCamera, viewComponent.Camera);
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
        var defaultView = Assert.IsType<GameObject2D>(Assert.Single(scene.Children));
        var defaultCamera = Assert.Single(defaultView.Components.OfType<StaticCamera>());
        var viewComponent = Assert.Single(defaultView.Components.OfType<ViewComponent>());
        var addedGameObjects = new List<IGameObject>();
        var addedComponents = new List<IComponent>();

        scene.GameObjectAdded += addedGameObjects.Add;
        scene.ComponentAdded += addedComponents.Add;

        scene.Activate();

        Assert.Contains(defaultView, addedGameObjects);
        Assert.Contains(addedComponents, component => ReferenceEquals(component, defaultCamera));
        Assert.Contains(addedComponents, component => ReferenceEquals(component, viewComponent));
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
    /// Verifies game objects are configured from the root of the tree to its leaves.
    /// </summary>
    [Fact]
    public void SetGameModel_configuresHierarchyFromRootToLeaf()
    {
        var root = new GameObject(10);
        var child = new GameObject(11);
        var leaf = new GameObject(12);
        root.AddChild(child);
        child.AddChild(leaf);
        var gameModel = new TestGameModel();

        root.SetGameModel(gameModel);

        Assert.Equal([root.Id, child.Id, leaf.Id], gameModel.RegistrationOrder);
    }

    /// <summary>
    /// Verifies spatial transforms compose through parents and ancestor changes notify descendants.
    /// </summary>
    [Fact]
    public void SpatialTransforms_composeAndTrackAncestorChanges()
    {
        var parent = new GameObject2D { Position = new Vector2D<float>(3f, 4f) };
        var child = new GameObject2D { Position = new Vector2D<float>(1f, 2f) };
        parent.AddChild(child);
        var worldTransformChanges = 0;
        child.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IGameObject2D.WorldTransform))
                worldTransformChanges++;
        };

        Assert.Equal(child.LocalTransform * parent.WorldTransform, child.WorldTransform);
        parent.Position = new Vector2D<float>(5f, 6f);
        Assert.Equal(0, worldTransformChanges);

        parent.Activate();
        worldTransformChanges = 0;
        var initialWorldTransform = child.WorldTransform;

        parent.Position = new Vector2D<float>(8f, 9f);

        Assert.NotEqual(initialWorldTransform, child.WorldTransform);
        Assert.Equal(1, worldTransformChanges);
    }

    /// <summary>
    /// Verifies spatial transforms and notifications pass through non-spatial game objects.
    /// </summary>
    [Fact]
    public void SpatialTransforms_inheritThroughNonSpatialAncestors()
    {
        var parent = new GameObject2D { Position = new Vector2D<float>(3f, 4f) };
        var intermediary = new GameObject();
        var child = new GameObject2D { Position = new Vector2D<float>(1f, 2f) };
        parent.AddChild(intermediary);
        intermediary.AddChild(child);
        var childWorldTransformChanges = 0;
        child.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IGameObject2D.WorldTransform))
                childWorldTransformChanges++;
        };

        Assert.Equal(child.LocalTransform * parent.WorldTransform, child.WorldTransform);
        parent.Position = new Vector2D<float>(5f, 6f);
        Assert.Equal(0, childWorldTransformChanges);

        parent.Activate();
        childWorldTransformChanges = 0;
        var initialWorldTransform = child.WorldTransform;

        parent.Position = new Vector2D<float>(8f, 9f);

        Assert.NotEqual(initialWorldTransform, child.WorldTransform);
        Assert.Equal(child.LocalTransform * parent.WorldTransform, child.WorldTransform);
        Assert.Equal(1, childWorldTransformChanges);

        var newParent = new GameObject2D { Position = new Vector2D<float>(12f, 13f) };
        Assert.True(parent.RemoveChild(intermediary));
        newParent.AddChild(intermediary);
        childWorldTransformChanges = 0;

        parent.Position = new Vector2D<float>(18f, 19f);
        Assert.Equal(0, childWorldTransformChanges);

        newParent.Activate();
        childWorldTransformChanges = 0;
        newParent.Position = new Vector2D<float>(20f, 21f);
        Assert.Equal(1, childWorldTransformChanges);
        Assert.Equal(child.LocalTransform * newParent.WorldTransform, child.WorldTransform);

        newParent.Deactivate();
        newParent.Position = new Vector2D<float>(22f, 23f);
        Assert.Equal(1, childWorldTransformChanges);
    }

    /// <summary>
    /// Verifies three-dimensional objects subscribe to spatial ancestors when activated.
    /// </summary>
    [Fact]
    public void SpatialTransforms3D_subscribeToNearestAncestorOnActivation()
    {
        var parent = new GameObject3D { Position = new Vector3D<float>(3f, 4f, 5f) };
        var intermediary = new GameObject();
        var child = new GameObject3D { Position = new Vector3D<float>(1f, 2f, 3f) };
        parent.AddChild(intermediary);
        intermediary.AddChild(child);
        var worldTransformChanges = 0;
        child.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IGameObject3D.WorldTransform))
                worldTransformChanges++;
        };

        parent.Position = new Vector3D<float>(6f, 7f, 8f);
        Assert.Equal(0, worldTransformChanges);

        parent.Activate();
        worldTransformChanges = 0;
        parent.Position = new Vector3D<float>(9f, 10f, 11f);

        Assert.Equal(1, worldTransformChanges);
        Assert.Equal(child.LocalTransform * parent.WorldTransform, child.WorldTransform);

        parent.Deactivate();
        parent.Position = new Vector3D<float>(12f, 13f, 14f);

        Assert.Equal(1, worldTransformChanges);
    }

    /// <summary>
    /// Verifies direct child collection changes notify listeners after the collection is updated.
    /// </summary>
    [Fact]
    public void AddChildAndRemoveChild_raisePropertyChangedForChildren()
    {
        var parent = new GameObject();
        var child = new GameObject();
        var changedProperties = new List<string?>();
        var childCountsAtNotification = new List<int>();
        parent.PropertyChanged += (_, args) =>
        {
            changedProperties.Add(args.PropertyName);
            if (args.PropertyName == nameof(IGameObject.Children))
                childCountsAtNotification.Add(parent.Children.Count);
        };

        parent.AddChild(child);
        Assert.True(parent.RemoveChild(child));
        Assert.False(parent.RemoveChild(child));

        Assert.Equal(
            [nameof(IGameObject.Children), nameof(IGameObject.Children)],
            changedProperties
        );
        Assert.Equal([1, 0], childCountsAtNotification);
    }

    /// <summary>
    /// Verifies active state propagates root-to-leaf before component notifications propagate leaf-to-root.
    /// </summary>
    [Fact]
    public void Activate_setsTreeStateTopDownAndNotifiesComponentsBottomUp()
    {
        var root = new GameObject(20);
        var child = new GameObject(21);
        var leaf = new GameObject(22);
        var rootComponent = new TestComponent();
        var childComponent = new TestComponent();
        var leafComponent = new TestComponent();
        root.AddChild(child);
        child.AddChild(leaf);
        root.AddComponent(rootComponent);
        child.AddComponent(childComponent);
        leaf.AddComponent(leafComponent);
        var activationOrder = new List<IComponent>();
        var entireTreeIsActiveAtNotification = true;

        root.ComponentAdded += component =>
        {
            activationOrder.Add(component);
            entireTreeIsActiveAtNotification &= root.IsActive && child.IsActive && leaf.IsActive;
        };

        root.Activate();

        Assert.Equal([leafComponent, childComponent, rootComponent], activationOrder);
        Assert.True(entireTreeIsActiveAtNotification);
    }

    /// <summary>
    /// Verifies adding initializes a component and object activation only notifies listeners.
    /// </summary>
    [Fact]
    public void AddComponent_initializesBeforeGameObjectActivationNotification()
    {
        var gameObject = new GameObject();
        var component = gameObject.AddComponent<TestComponent>();
        Assert.Equal(1, component.InitializationCount);
        Assert.False(component.IsActivated);
        var initializedAtNotification = false;
        var activeAtNotification = true;
        gameObject.ComponentAdded += addedComponent =>
        {
            if (!ReferenceEquals(addedComponent, component))
                return;

            initializedAtNotification = component.InitializationCount == 1;
            activeAtNotification = component.IsActivated;
        };

        gameObject.Activate();
        gameObject.Deactivate();
        gameObject.Activate();

        Assert.Equal(1, component.InitializationCount);
        Assert.True(initializedAtNotification);
        Assert.False(activeAtNotification);
        Assert.False(component.IsActivated);
    }

    /// <summary>
    /// Verifies a component added to an active object initializes without changing activation state.
    /// </summary>
    [Fact]
    public void AddComponent_toActiveObject_initializesBeforeNotification()
    {
        var gameObject = new GameObject();
        gameObject.Activate();
        var component = new TestComponent();
        var activeAtNotification = true;
        gameObject.ComponentAdded += addedComponent =>
        {
            if (ReferenceEquals(addedComponent, component))
                activeAtNotification = component.IsActivated;
        };

        gameObject.AddComponent(component);

        Assert.Equal(1, component.InitializationCount);
        Assert.False(component.IsActivated);
        Assert.False(activeAtNotification);
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

        /// <summary>Gets the order in which game objects were registered.</summary>
        public List<GameObjectId> RegistrationOrder { get; } = [];

        /// <inheritdoc/>
        public IGameObject? GetGameObject(GameObjectId gameObjectId) =>
            _gameObjects.GetValueOrDefault(gameObjectId);

        /// <inheritdoc/>
        public void RegisterGameObject(IGameObject gameObject)
        {
            _gameObjects[gameObject.Id] = gameObject;
            RegistrationOrder.Add(gameObject.Id);
        }

        /// <inheritdoc/>
        public void UnregisterGameObject(IGameObject gameObject) =>
            _gameObjects.Remove(gameObject.Id);
    }

    /// <summary>
    /// Provides a concrete component for ownership tests.
    /// </summary>
    private sealed class TestComponent : Component
    {
        /// <summary>Gets the number of times this component has initialized.</summary>
        public int InitializationCount { get; private set; }

        /// <inheritdoc />
        public override string DisplayName => "Test Component";

        /// <inheritdoc />
        protected override void OnInitialize() => InitializationCount++;
    }
}
