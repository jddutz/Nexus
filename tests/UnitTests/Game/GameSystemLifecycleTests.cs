namespace Tests;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.GameEngine;
using Nexus.Graphics;
using Nexus.Graphics.Events;
using Silk.NET.Maths;
using Silk.NET.Windowing;

/// <summary>
/// Verifies parent-first lifecycle traversal and mutation handling in the game system.
/// </summary>
public class GameSystemLifecycleTests
{
    /// <summary>
    /// Verifies the scene updates once before game objects, components, and child objects.
    /// </summary>
    [Fact]
    public void Update_runsSceneAndDependentsInParentFirstOrder()
    {
        var calls = new List<string>();
        var scene = new LifecycleScene(calls)
        {
        };
        var parent = new LifecycleGameObject("parent", calls);
        var component = new LifecycleComponent("component", calls);
        var child = new LifecycleGameObject("child", calls);
        parent.AddComponent(component);
        parent.Children.Add(child);
        scene.Children.Add(parent);
        var gameSystem = CreateGameSystem(scene);
        gameSystem.Initialize();
        gameSystem.Update(0);
        calls.Clear();

        gameSystem.Update(0.25);

        Assert.Equal(["scene.update", "parent.update", "component.update", "child.update"], calls);
    }

    /// <summary>
    /// Verifies inactive parents block activation and updates but not initialization.
    /// </summary>
    [Fact]
    public void Update_initializesUnderInactiveParentAndRetriesActivation()
    {
        var calls = new List<string>();
        var scene = new LifecycleScene(calls)
        {
        };
        var parent = new LifecycleGameObject("parent", calls) { ActivationAllowed = false };
        var child = new LifecycleGameObject("child", calls);
        var component = new LifecycleComponent("component", calls);
        parent.AddComponent(component);
        parent.Children.Add(child);
        scene.Children.Add(parent);
        var gameSystem = CreateGameSystem(scene);
        gameSystem.Initialize();
        gameSystem.Update(0);

        Assert.Equal(1, parent.InitializeCount);
        Assert.Equal(1, child.InitializeCount);
        Assert.Equal(1, component.InitializeCount);
        Assert.False(parent.IsActivated);
        Assert.False(child.IsActivated);
        Assert.False(component.IsActivated);

        parent.ActivationAllowed = true;
        gameSystem.Update(0.25);

        Assert.True(parent.IsActivated);
        Assert.True(child.IsActivated);
        Assert.True(component.IsActivated);

        parent.ActivationAllowed = false;
        parent.Activate();
        Assert.True(parent.IsActivated);

        gameSystem.Update(0.25);

        Assert.True(parent.IsActivated);
        Assert.Equal(2, parent.UpdateCount);
        Assert.Equal(2, child.UpdateCount);
        Assert.Equal(2, component.UpdateCount);
        Assert.Equal(0, parent.DeactivationCount);
    }

    /// <summary>
    /// Verifies a removed and re-added entity reactivates immediately but skips the current traversal.
    /// </summary>
    [Fact]
    public void Update_skipsRemovedAndReaddedSubtreeUntilNextFrame()
    {
        var calls = new List<string>();
        var scene = new LifecycleScene(calls)
        {
        };
        var parent = new LifecycleGameObject("parent", calls);
        var child = new LifecycleGameObject("child", calls);
        var component = new LifecycleComponent("component", calls);
        child.AddComponent(component);
        parent.Children.Add(child);
        scene.Children.Add(parent);
        var gameSystem = CreateGameSystem(scene);
        gameSystem.Initialize();
        gameSystem.Update(0);
        parent.OnUpdate = () =>
        {
            parent.OnUpdate = null;
            Assert.True(parent.Children.Remove(child));
            parent.Children.Add(child);
        };

        gameSystem.Update(0.25);

        Assert.True(child.IsActivated);
        Assert.True(component.IsActivated);
        Assert.Equal(1, child.UpdateCount);
        Assert.Equal(1, component.UpdateCount);
        Assert.Equal(1, child.InitializeCount);
        Assert.Equal(1, component.InitializeCount);

        gameSystem.Update(0.25);

        Assert.True(child.IsActivated);
        Assert.True(component.IsActivated);
        Assert.Equal(2, child.UpdateCount);
        Assert.Equal(2, component.UpdateCount);
        Assert.Equal(1, child.InitializeCount);
        Assert.Equal(1, component.InitializeCount);
    }

    /// <summary>
    /// Verifies additions under active parents activate immediately and update on the next frame.
    /// </summary>
    [Fact]
    public void Update_activatesAddedChildImmediatelyAndUpdatesNextFrame()
    {
        var calls = new List<string>();
        var scene = new LifecycleScene(calls)
        {
        };
        var parent = new LifecycleGameObject("parent", calls);
        var child = new LifecycleGameObject("child", calls);
        scene.Children.Add(parent);
        var gameSystem = CreateGameSystem(scene);
        gameSystem.Initialize();
        parent.OnUpdate = () =>
        {
            parent.OnUpdate = null;
            parent.Children.Add(child);
        };

        gameSystem.Update(0.25);

        Assert.True(child.IsInitialized);
        Assert.True(child.IsActivated);
        Assert.Equal(0, child.UpdateCount);
        Assert.Equal(1, child.ActivationCount);

        gameSystem.Update(0.25);

        Assert.True(child.IsInitialized);
        Assert.True(child.IsActivated);
        Assert.Equal(1, child.UpdateCount);
        Assert.Equal(1, child.ActivationCount);
    }

    /// <summary>Verifies child activation subscriptions are installed before parent activation.</summary>
    [Fact]
    public void ActivateGameObject_subscribesBeforeActivationAndPublishesEachNodeOnce()
    {
        var calls = new List<string>();
        var scene = new LifecycleScene(calls)
        {
        };
        var eventHub = new EventHub();
        var root = new LifecycleGameObject("root", calls);
        var child = new LifecycleGameObject("child", calls);
        root.OnActivation = () => root.Children.Add(child);
        scene.Children.Add(root);
        var gameSystem = CreateGameSystem(scene, eventHub: eventHub);
        gameSystem.Initialize();

        Assert.True(root.IsActivated);
        Assert.True(child.IsActivated);
        Assert.Equal(1, root.ActivationCount);
        Assert.Equal(1, child.ActivationCount);
        Assert.Equal(0, child.UpdateCount);

        eventHub.Drain();

        Assert.Equal(
            ["root.activated-event", "child.activated-event"],
            calls.Where(call => call.EndsWith(".activated-event", StringComparison.Ordinal))
        );

        gameSystem.Update(0.25);

        Assert.Equal(1, child.ActivationCount);
        Assert.Equal(1, child.UpdateCount);
    }

    /// <summary>
    /// Verifies moving initialized objects and components preserves initialization state.
    /// </summary>
    [Fact]
    public void ActiveScene_transferPreservesInitializationState()
    {
        var calls = new List<string>();
        var scene = new LifecycleScene(calls)
        {
        };
        var source = new LifecycleGameObject("source", calls);
        var destination = new LifecycleGameObject("destination", calls);
        var movedObject = new LifecycleGameObject("moved", calls);
        var movedComponent = new LifecycleComponent("moved-component", calls);
        movedObject.AddComponent(movedComponent);
        source.Children.Add(movedObject);
        scene.Children.Add(source);
        scene.Children.Add(destination);
        var eventHub = new EventHub();
        var gameSystem = CreateGameSystem(scene, eventHub: eventHub);
        gameSystem.Initialize();
        gameSystem.Update(0);

        Assert.True(source.Children.Remove(movedObject));
        destination.Children.Add(movedObject);
        Assert.True(movedObject.RemoveComponent(movedComponent));
        destination.AddComponent(movedComponent);
        eventHub.Drain();
        gameSystem.Update(0);

        Assert.True(movedObject.IsActivated);
        Assert.True(movedComponent.IsActivated);
        Assert.Equal(1, movedObject.InitializeCount);
        Assert.Equal(1, movedComponent.InitializeCount);
    }

    /// <summary>
    /// Creates a game system whose initial scene is the supplied scene.
    /// </summary>
    /// <param name="scene">The scene to activate.</param>
    /// <returns>A game system registered with the scene.</returns>
    private static Game CreateGameSystem(
        IScene scene,
        IEventHub? eventHub = null
    )
    {
        var gameSystem = new Game(
            eventHub ?? new EventHub(),
            NullLogger<Game>.Instance
        );
        gameSystem.LoadScene(scene);
        return gameSystem;
    }

    /// <summary>
    /// Scene that records its frame update.
    /// </summary>
    /// <param name="calls">The list recording lifecycle calls.</param>
    private sealed class LifecycleScene(List<string> calls)
        : Scene(NodeId.New(), SceneRegistryTestHelper.ServiceProvider)
    {
        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            calls.Add("scene.update");
        }
    }

    /// <summary>
    /// Game object that records lifecycle calls and supports activation and update callbacks.
    /// </summary>
    private sealed class LifecycleGameObject(string name, List<string> calls) : GameObject
    {
        /// <summary>Gets or sets whether this object currently permits activation.</summary>
        public bool ActivationAllowed { get; set; } = true;

        /// <summary>Gets or sets a callback invoked during this object's update.</summary>
        public Action? OnUpdate { get; set; }

        /// <summary>Gets or sets a callback invoked during activation.</summary>
        public Action? OnActivation { get; set; }

        /// <summary>Gets the number of initialization calls.</summary>
        public int InitializeCount { get; private set; }

        /// <summary>Gets the number of activation calls.</summary>
        public int ActivationCount { get; private set; }

        /// <summary>Gets the number of update calls.</summary>
        public int UpdateCount { get; private set; }

        /// <summary>Gets the number of deactivation calls.</summary>
        public int DeactivationCount { get; private set; }

        /// <inheritdoc />
        public override void Initialize()
        {
            InitializeCount++;
            calls.Add($"{name}.initialize");
            base.Initialize();
        }

        /// <inheritdoc />
        public override bool CanActivate() => ActivationAllowed;

        /// <inheritdoc />
        public override void Activate()
        {
            ActivationCount++;
            calls.Add($"{name}.activate");
            base.Activate();
        }

        /// <inheritdoc />
        protected override void OnActivated() => OnActivation?.Invoke();

        /// <summary>Records a game-object activation event targeted at this object.</summary>
        /// <param name="message">The game-object activation event.</param>
        public void Handle(GameObjectActivatedEvent message)
        {
            if (ReferenceEquals(message.GameObject, this))
                calls.Add($"{name}.activated-event");
        }

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            UpdateCount++;
            calls.Add($"{name}.update");
            OnUpdate?.Invoke();
        }

        /// <inheritdoc />
        public override void Deactivate()
        {
            if (IsActivated)
                DeactivationCount++;
            calls.Add($"{name}.deactivate");
            base.Deactivate();
        }
    }

    /// <summary>
    /// Component that records lifecycle calls for traversal and transfer tests.
    /// </summary>
    private sealed class LifecycleComponent(string name, List<string> calls) : Component
    {
        /// <summary>Gets the number of initialization calls.</summary>
        public int InitializeCount { get; private set; }

        /// <summary>Gets the number of update calls.</summary>
        public int UpdateCount { get; private set; }

        /// <inheritdoc />
        public override void Initialize()
        {
            InitializeCount++;
            calls.Add($"{name}.initialize");
            base.Initialize();
        }

        /// <inheritdoc />
        public override void Activate()
        {
            calls.Add($"{name}.activate");
            base.Activate();
        }

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            UpdateCount++;
            calls.Add($"{name}.update");
        }

        /// <inheritdoc />
        public override void Deactivate()
        {
            calls.Add($"{name}.deactivate");
            base.Deactivate();
        }
    }
}
