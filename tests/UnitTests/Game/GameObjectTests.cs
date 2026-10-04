using Nexus.Core;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Silk.NET.Maths;

namespace Tests;

/// <summary>
/// Tests game object component ownership and scene hierarchy behavior.
/// </summary>
public class GameObjectTests
{
    /// <summary>
    /// Verifies that a scene has its own identity and cannot be attached to a game object.
    /// </summary>
    [Fact]
    public void Scene_isTopLevelEntityWithNodeId()
    {
        var nodeId = new NodeId(1);
        var scene = new Scene(nodeId);
        var defaultView = Assert.IsType<GameObject2D>(Assert.Single(scene.Children));
        var defaultCamera = Assert.IsType<StaticCamera>(
            Assert.Single(defaultView.Components.OfType<StaticCamera>())
        );
        var viewComponent = Assert.Single(defaultView.Components.OfType<ViewRenderer>());

        Assert.Equal(nodeId, scene.Id);
        Assert.Same(scene, defaultView.Parent);
        Assert.Same(scene, defaultView.Root);
        Assert.Same(scene, scene.GetSceneNode(scene.Id));
        Assert.Same(defaultView, scene.GetSceneNode(defaultView.Id));
        Assert.Contains(defaultView, scene.Children);
        Assert.Contains(
            defaultView.Components,
            component => ReferenceEquals(component, defaultCamera)
        );
        Assert.Same(defaultCamera, viewComponent.Camera);
        Assert.IsNotAssignableFrom<IGameObject>(scene);
        var view = new View();
        Assert.Equal(nameof(ViewRenderer), (string)view.ViewComponent.Name);
        Assert.Equal(RenderPasses.Main, (uint)view.ViewComponent.RenderPassMask);
        Assert.Contains(view.ViewComponent, view.Components);
    }

    /// <summary>Verifies components are exposed as a read-only observable collection.</summary>
    [Fact]
    public void Components_exposeReadOnlyObservableView()
    {
        var gameObject = new GameObject();
        IGameObject gameObjectContract = gameObject;
        var components = gameObjectContract.Components;
        var addedItems = new List<IComponent>();
        var removedItems = new List<IComponent>();
        components.ItemAdded += addedItems.Add;
        components.ItemRemoved += removedItems.Add;

        var component = new TestComponent();
        gameObject.AddComponent(component);

        Assert.Same(component, components[0]);
        Assert.Single(components);
        Assert.Same(component, Assert.Single(addedItems));
        Assert.True(gameObject.RemoveComponent(component));
        Assert.Empty(components);
        Assert.Same(component, Assert.Single(removedItems));

        var existingComponent = new TestComponent();
        gameObject.AddComponent(existingComponent);

        Assert.Contains(existingComponent, components);
        Assert.True(gameObject.RemoveComponent(existingComponent));
        Assert.Equal(2, addedItems.Count);
        Assert.Same(existingComponent, addedItems[1]);
        Assert.Equal(2, removedItems.Count);
        Assert.Same(existingComponent, removedItems[1]);
        Assert.IsNotAssignableFrom<IObservableCollection<IComponent>>(gameObject);
        Assert.IsNotAssignableFrom<IEnumerable<IComponent>>(gameObject);
    }

    /// <summary>
    /// Verifies adding and removing children updates their parent reference.
    /// </summary>
    [Fact]
    public void ChildrenCollection_updatesChildParent()
    {
        var parent = new GameObject();
        var child = new GameObject();
        var addedChildren = new List<ISceneNode>();
        parent.Children.ItemAdded += addedChildren.Add;

        parent.Children.Add(child);
        Assert.Throws<InvalidOperationException>(() => parent.Children.Add(child));

        Assert.Same(parent, child.Parent);
        Assert.Single(addedChildren);
        Assert.All(addedChildren, addedChild => Assert.Same(child, addedChild));

        Assert.True(parent.Children.Remove(child));
        Assert.Null(child.Parent);
    }

    /// <summary>Verifies child validation rejects null, owned, self, and ancestor nodes.</summary>
    [Fact]
    public void ChildrenValidation_rejectsInvalidOwnershipAndCycles()
    {
        var parent = new GameObject();
        var child = new GameObject();
        var otherParent = new GameObject();
        var scene = new Scene();
        parent.Children.Add(child);

        Assert.Throws<InvalidOperationException>(() => parent.Children.Add(null!));
        Assert.Throws<InvalidOperationException>(() => otherParent.Children.Add(child));
        Assert.Throws<InvalidOperationException>(() => parent.Children.Add(parent));
        Assert.Throws<InvalidOperationException>(() => parent.Children.Add(scene));
        Assert.Throws<InvalidOperationException>(() => child.Children.Add(parent));

        Assert.Same(parent, child.Parent);
        Assert.Single(parent.Children);
        Assert.Empty(otherParent.Children);
        Assert.Empty(child.Children);
        Assert.Null(scene.Parent);

        var otherChild = new GameObject();
        otherParent.Children.Add(otherChild);
        Assert.Same(otherParent, otherChild.Parent);
        Assert.Same(otherChild, Assert.Single(otherParent.Children));
    }

    /// <summary>Verifies child membership and removal use reference identity.</summary>
    [Fact]
    public void ChildrenCollection_usesReferenceIdentity()
    {
        var parent = new GameObject();
        var first = new EqualGameObject();
        var second = new EqualGameObject();

        parent.Children.Add(first);
        parent.Children.Add(second);

        Assert.Equal(2, parent.Children.Count);
        Assert.True(parent.Children.Remove(second));
        Assert.Same(parent, first.Parent);
        Assert.Null(second.Parent);
        Assert.Same(first, Assert.Single(parent.Children));
    }

    /// <summary>
    /// Verifies scene activation raises notifications without activating its default view and camera.
    /// </summary>
    [Fact]
    public void Scene_activationChangesOnlySceneState()
    {
        var scene = new Scene();
        var defaultView = Assert.IsType<GameObject2D>(Assert.Single(scene.Children));
        var defaultCamera = Assert.Single(defaultView.Components.OfType<StaticCamera>());
        var viewComponent = Assert.Single(defaultView.Components.OfType<ViewRenderer>());
        var activationNotifications = 0;
        scene.PropertyChanged += propertyName =>
        {
            if (propertyName == nameof(IManagedEntity.IsActivated))
                activationNotifications++;
        };

        scene.Activate();

        Assert.True(scene.IsActive);
        Assert.Equal(1, activationNotifications);
        Assert.False(defaultView.IsActivated);
        Assert.False(defaultCamera.IsActivated);
        Assert.False(viewComponent.IsActivated);

        scene.Deactivate();

        Assert.Equal(2, activationNotifications);
    }

    /// <summary>
    /// Verifies that constructor-supplied components receive their owning game object.
    /// </summary>
    [Fact]
    public void ConstructorComponents_assignOwner()
    {
        var component = new TestComponent();
        var gameObject = new GameObject(1, [component]);

        Assert.Same(gameObject, component.Owner);
    }

    /// <summary>
    /// Verifies Scene registers existing descendants and tracks later subtree changes.
    /// </summary>
    [Fact]
    public void Scene_tracksAllNodesWhenSubtreesAreAddedAndRemoved()
    {
        var root = new GameObject(10);
        var child = new GameObject(11);
        var leaf = new GameObject(12);
        root.AddChild(child);
        child.AddChild(leaf);
        var scene = new Scene();

        scene.Children.Add(root);

        Assert.Same(scene, root.Parent);
        Assert.Same(scene, root.Root);
        Assert.Same(scene, child.Root);
        Assert.Same(scene, leaf.Root);
        Assert.Same(root, scene.GetSceneNode(root.Id));
        Assert.Same(child, scene.GetSceneNode(child.Id));
        Assert.Same(leaf, scene.GetSceneNode(leaf.Id));

        var laterChild = new GameObject(13);
        child.AddChild(laterChild);
        Assert.Same(laterChild, scene.GetSceneNode(laterChild.Id));

        Assert.True(scene.Children.Remove(root));

        Assert.Null(scene.GetSceneNode(root.Id));
        Assert.Null(scene.GetSceneNode(child.Id));
        Assert.Null(scene.GetSceneNode(leaf.Id));
        Assert.Null(scene.GetSceneNode(laterChild.Id));
        Assert.Null(root.Root);
        Assert.Null(root.Parent);
        Assert.Same(child, leaf.Parent);
    }

    /// <summary>Verifies scene ID collisions reject complete subtrees before insertion.</summary>
    [Fact]
    public void Scene_rejectsDuplicateIdsBeforeMutatingCollections()
    {
        var scene = new Scene(new NodeId(500));
        var existingNode = new GameObject(501);
        scene.Children.Add(existingNode);

        var candidate = new GameObject(502);
        var collidingDescendant = new GameObject(501);
        candidate.Children.Add(collidingDescendant);

        Assert.Throws<InvalidOperationException>(() => scene.Children.Add(candidate));

        Assert.Null(candidate.Parent);
        Assert.Null(candidate.Root);
        Assert.Same(existingNode, scene.GetSceneNode(existingNode.Id));
        Assert.Null(scene.GetSceneNode(candidate.Id));
        Assert.DoesNotContain(candidate, scene.Children);
        Assert.Same(candidate, collidingDescendant.Parent);

        var duplicateIdSubtree = new GameObject(503);
        duplicateIdSubtree.Children.Add(new GameObject(504));
        duplicateIdSubtree.Children.Add(new GameObject(504));

        Assert.Throws<InvalidOperationException>(() => scene.Children.Add(duplicateIdSubtree));

        Assert.Null(duplicateIdSubtree.Parent);
        Assert.Null(duplicateIdSubtree.Root);
        Assert.DoesNotContain(duplicateIdSubtree, scene.Children);
    }

    /// <summary>Verifies nested additions validate IDs against the containing scene.</summary>
    [Fact]
    public void Scene_rejectsDuplicateIdsAddedUnderRegisteredGameObjects()
    {
        var scene = new Scene(new NodeId(600));
        var registeredParent = new GameObject(601);
        var existingNode = new GameObject(602);
        scene.Children.Add(registeredParent);
        scene.Children.Add(existingNode);
        var collidingChild = new GameObject(602);

        Assert.Throws<InvalidOperationException>(() =>
            registeredParent.Children.Add(collidingChild)
        );

        Assert.Null(collidingChild.Parent);
        Assert.Null(collidingChild.Root);
        Assert.DoesNotContain(collidingChild, registeredParent.Children);
        Assert.Same(existingNode, scene.GetSceneNode(existingNode.Id));
    }

    /// <summary>
    /// Verifies a node must be detached before it can be added to another scene.
    /// </summary>
    [Fact]
    public void Scene_addsNodeByRemovingItFromItsPreviousScene()
    {
        var node = new GameObject(20);
        var child = new GameObject(21);
        var leaf = new GameObject(22);
        node.Children.Add(child);
        child.Children.Add(leaf);
        var previousScene = new Scene(new NodeId(100));
        var nextScene = new Scene(new NodeId(101));
        previousScene.Children.Add(node);

        Assert.Throws<InvalidOperationException>(() => nextScene.Children.Add(node));
        Assert.Same(previousScene, node.Parent);

        ISceneNode rootNode = node;
        rootNode.Parent = null;

        Assert.DoesNotContain(node, previousScene.Children);
        Assert.Null(previousScene.GetSceneNode(node.Id));
        Assert.Null(previousScene.GetSceneNode(child.Id));
        Assert.Null(previousScene.GetSceneNode(leaf.Id));
        Assert.Same(node, child.Parent);
        Assert.Same(child, leaf.Parent);

        nextScene.Children.Add(rootNode);

        Assert.Same(nextScene, node.Root);
        Assert.Same(nextScene, node.Parent);
        Assert.Same(node, nextScene.GetSceneNode(node.Id));
        Assert.Same(nextScene, child.Root);
        Assert.Same(nextScene, leaf.Root);
    }

    /// <summary>Verifies Scene child validation rejects null nodes and ancestor cycles.</summary>
    [Fact]
    public void SceneChildrenValidation_rejectsNullAndCycles()
    {
        var scene = new Scene();
        var child = new GameObject();
        scene.Children.Add(child);

        Assert.Throws<InvalidOperationException>(() => scene.Children.Add(null!));
        Assert.Throws<InvalidOperationException>(() => scene.Children.Add(scene));
        var otherScene = new Scene();
        Assert.Throws<InvalidOperationException>(() => scene.Children.Add(otherScene));
        Assert.Throws<InvalidOperationException>(() => child.Children.Add(scene));
        Assert.Throws<InvalidOperationException>(() => otherScene.Parent = scene);

        Assert.Same(scene, child.Parent);
        Assert.Contains(child, scene.Children);
        Assert.Empty(child.Children);
        Assert.Null(otherScene.Parent);
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
        child.PropertyChanged += propertyName =>
        {
            if (propertyName == nameof(IGameObject2D.WorldTransform))
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
        child.PropertyChanged += propertyName =>
        {
            if (propertyName == nameof(IGameObject2D.WorldTransform))
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
        child.PropertyChanged += propertyName =>
        {
            if (propertyName == nameof(IGameObject3D.WorldTransform))
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

    /// <summary>Verifies that direct child collection changes do not raise property notifications.</summary>
    [Fact]
    public void AddChildAndRemoveChild_doNotRaisePropertyChangedForChildren()
    {
        var parent = new GameObject();
        var child = new GameObject();
        var changedProperties = new List<string?>();
        var childCountsAtNotification = new List<int>();
        parent.PropertyChanged += propertyName =>
        {
            changedProperties.Add(propertyName);
            if (propertyName == nameof(IGameObject.Children))
                childCountsAtNotification.Add(parent.Children.Count);
        };

        parent.AddChild(child);
        Assert.True(parent.RemoveChild(child));
        Assert.False(parent.RemoveChild(child));

        Assert.Empty(changedProperties);
        Assert.Empty(childCountsAtNotification);
    }

    /// <summary>
    /// Verifies activation changes only this object's state and components.
    /// </summary>
    [Fact]
    public void Activate_activatesOnlyThisObjectAndItsComponents()
    {
        var rootComponent = new TestComponent();
        var childComponent = new TestComponent();
        var leafComponent = new TestComponent();
        var root = new GameObject(20, [rootComponent]);
        var child = new GameObject(21, [childComponent]);
        var leaf = new GameObject(22, [leafComponent]);
        root.AddChild(child);
        child.AddChild(leaf);
        var activationOrder = new List<IComponent>();

        root.ComponentAdded += component =>
        {
            activationOrder.Add(component);
        };

        root.Activate();

        Assert.Equal([rootComponent], activationOrder);
        Assert.True(root.IsActivated);
        Assert.False(child.IsActivated);
        Assert.False(leaf.IsActivated);
        Assert.True(rootComponent.IsActivated);
    }

    /// <summary>
    /// Verifies components initialize once and are active when object activation callbacks run.
    /// </summary>
    [Fact]
    public void ConstructorComponents_initializeOnceBeforeGameObjectActivationNotification()
    {
        var component = new TestComponent();
        var gameObject = new GameObject([component]);
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
        Assert.True(activeAtNotification);
        Assert.True(component.IsActivated);
    }

    /// <summary>
    /// Verifies the constructor snapshots its input and exposes components through a read-only view.
    /// </summary>
    [Fact]
    public void ConstructorComponents_areImmutableAfterConstruction()
    {
        var component = new TestComponent();
        var suppliedComponents = new List<IComponent> { component };
        var gameObject = new GameObject(suppliedComponents);
        suppliedComponents.Clear();

        Assert.Same(component, Assert.Single(gameObject.Components));
        Assert.IsNotAssignableFrom<IList<IComponent>>(gameObject.Components);
        Assert.IsNotAssignableFrom<IComponent[]>(gameObject.Components);

        Assert.Equal(1, component.InitializationCount);
        Assert.False(component.IsActivated);
    }

    /// <summary>Verifies runtime additions and removals raise active component lifecycle events.</summary>
    [Fact]
    public void RuntimeComponentChanges_addAndRemoveComponents()
    {
        var gameObject = new GameObject();
        var component = new TestComponent();
        var addedComponents = new List<IComponent>();
        var removedComponents = new List<IComponent>();
        gameObject.ComponentAdded += addedComponents.Add;
        gameObject.ComponentRemoved += removedComponents.Add;
        gameObject.Activate();

        gameObject.AddComponent(component);

        Assert.Same(component, Assert.Single(gameObject.Components));
        Assert.Equal(1, component.InitializationCount);
        Assert.Same(gameObject, component.Owner);
        Assert.Same(component, Assert.Single(addedComponents));

        Assert.True(gameObject.RemoveComponent(component));

        Assert.Empty(gameObject.Components);
        Assert.Null(component.Owner);
        Assert.Same(component, Assert.Single(removedComponents));
        Assert.False(gameObject.RemoveComponent(component));
    }

    /// <summary>Verifies generic component helpers create and remove the requested type.</summary>
    [Fact]
    public void GenericComponentMethods_createAndRemoveRequestedType()
    {
        var gameObject = new GameObject();

        var component = gameObject.AddComponent<TestComponent>();

        Assert.Same(component, gameObject.GetComponent<TestComponent>());
        Assert.True(gameObject.RemoveComponent<TestComponent>());
        Assert.Null(gameObject.GetComponent<TestComponent>());
    }

    /// <summary>Verifies the observable collection exposes generic add, lookup, and removal.</summary>
    [Fact]
    public void ObservableCollectionMethods_createFindAndRemoveItems()
    {
        IObservableCollection<IComponent> components = new ObservableCollection<IComponent>();
        var addedItems = new List<IComponent>();
        var removedItems = new List<IComponent>();
        components.ItemAdded += addedItems.Add;
        components.ItemRemoved += removedItems.Add;

        var component = components.Add<TestComponent>();

        Assert.Same(component, components.Get<TestComponent>());
        Assert.Same(component, components[0]);
        Assert.Single(components);
        Assert.Same(component, Assert.Single(addedItems));
        Assert.True(components.Remove<TestComponent>());
        Assert.Null(components.Get<TestComponent>());
        Assert.Empty(components);
        Assert.Same(component, Assert.Single(removedItems));
    }

    /// <summary>
    /// Verifies that a component already owned by another object cannot be reused.
    /// </summary>
    [Fact]
    public void Constructor_rejectsComponentAlreadyOwnedByAnotherObject()
    {
        var component = new TestComponent();
        var previousOwner = new GameObject(1, [component]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new GameObject(2, [component])
        );

        Assert.Contains(component, previousOwner.Components);
        Assert.Same(previousOwner, component.Owner);
        Assert.Contains("one game object", exception.Message);
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
        public override void Initialize()
        {
            if (IsInitialized)
                return;

            InitializationCount++;
            base.Initialize();
        }
    }

    /// <summary>Provides game objects that compare equal regardless of instance identity.</summary>
    private sealed class EqualGameObject : GameObject
    {
        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is EqualGameObject;

        /// <inheritdoc />
        public override int GetHashCode() => 0;
    }
}
