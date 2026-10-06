namespace Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;

/// <summary>
/// Verifies game objects and components participate in global event handling with their lifecycle.
/// </summary>
public class GameSystemEventRegistrationTests
{
    /// <summary>
    /// Verifies Game settings bind from the Game configuration section.
    /// </summary>
    [Fact]
    public void AddGameServices_bindsGameSettingsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Game:StartSceneId"] = "ConfiguredScene" }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddGameServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<IOptions<GameSettings>>().Value;

        Assert.Equal("ConfiguredScene", settings.StartSceneId);
    }

    /// <summary>Verifies scene registry settings use their discovery defaults.</summary>
    [Fact]
    public void AddGameServices_registersSceneRegistrySettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Game:StartSceneId"] = "ConfiguredScene" }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddGameServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<IOptions<SceneRegistrySettings>>().Value;

        Assert.True(settings.ScanEntryAssembly);
    }

    /// <summary>
    /// Verifies loading activates the scene hierarchy and registers its event handlers.
    /// </summary>
    [Fact]
    public void LoadScene_activatesTheConfiguredInitialScene()
    {
        var scene = new Scene(NodeId.New())
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
        var rootComponent = new EventHandlingComponent();
        var root = new EventHandlingGameObject([rootComponent]);
        var childComponent = new EventHandlingComponent();
        var child = new EventHandlingGameObject([childComponent]);
        root.AddChild(child);
        scene.Children.Add(root);
        var gameSystem = CreateGameSystem(new EventHub());
        gameSystem.LoadScene(scene);

        gameSystem.Initialize();

        Assert.Same(scene, gameSystem.CurrentScene);
        Assert.True(gameSystem.IsSceneLoaded);
        Assert.True(root.IsActivated);
        Assert.True(child.IsActivated);
        Assert.True(rootComponent.IsActivated);
        Assert.True(childComponent.IsActivated);
    }

    /// <summary>
    /// Verifies an active scene tracks lifecycle changes through its child and component collections.
    /// </summary>
    [Fact]
    public void ActiveScene_tracksLifecycleForDynamicallyChangedHierarchy()
    {
        var scene = new Scene(NodeId.New())
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
        var eventHub = new EventHub();
        var gameSystem = CreateGameSystem(eventHub);
        gameSystem.LoadScene(scene);
        gameSystem.Initialize();

        var rootComponent = new EventHandlingComponent();
        var root = new EventHandlingGameObject([rootComponent]);
        scene.Children.Add(root);

        Assert.True(root.IsActivated);
        Assert.True(rootComponent.IsActivated);
        eventHub.Drain();

        var childComponent = new EventHandlingComponent();
        var child = new EventHandlingGameObject([childComponent]);
        root.AddChild(child);

        Assert.True(child.IsActivated);
        Assert.True(childComponent.IsActivated);
        Assert.Equal(0, child.GameObjectActivationCount);
        eventHub.Drain();
        Assert.Equal(1, child.GameObjectActivationCount);
        gameSystem.Update(0);
        Assert.True(child.IsActivated);
        Assert.True(childComponent.IsActivated);

        var addedComponent = new EventHandlingComponent();
        child.AddComponent(addedComponent);
        Assert.False(addedComponent.IsActivated);
        eventHub.Drain();
        Assert.True(addedComponent.IsActivated);

        Assert.True(child.RemoveComponent(addedComponent));
        Assert.False(addedComponent.IsActivated);

        Assert.True(root.RemoveChild(child));
        Assert.False(child.IsActivated);
        Assert.False(childComponent.IsActivated);
        Assert.Null(scene.GetSceneNode(child.Id));

        Assert.True(scene.Children.Remove(root));
        Assert.False(root.IsActivated);
        Assert.False(rootComponent.IsActivated);
        Assert.Null(scene.GetSceneNode(root.Id));
    }

    /// <summary>
    /// Verifies initialization is independent of scene selection.
    /// </summary>
    [Fact]
    public void Initialize_succeedsWhenNoSceneHasBeenLoaded()
    {
        var gameSystem = CreateGameSystem(new EventHub());

        gameSystem.Initialize();

        Assert.Null(gameSystem.CurrentScene);
        Assert.False(gameSystem.IsSceneLoaded);
    }

    /// <summary>
    /// Verifies GameSystem activation wires an object tree and deactivation removes its handlers.
    /// </summary>
    [Fact]
    public void ActivateGameObjects_registersAndUnregistersGlobalEventHandlers()
    {
        var eventHub = new EventHub();
        var gameSystem = CreateGameSystem(eventHub);
        var rootComponent = new EventHandlingComponent();
        var root = new EventHandlingGameObject([rootComponent]);
        var childComponent = new EventHandlingComponent();
        var child = new EventHandlingGameObject([childComponent]);
        root.AddChild(child);

        gameSystem.ActivateGameObject(root);
        gameSystem.ActivateGameObject(child);
        gameSystem.ActivateComponent(rootComponent);
        gameSystem.ActivateComponent(childComponent);
        eventHub.Publish(new ProbeEvent());
        eventHub.Drain();

        Assert.Equal(1, root.GlobalEventCount);
        Assert.Equal(1, child.GlobalEventCount);
        Assert.Equal(1, rootComponent.GlobalEventCount);
        Assert.Equal(1, childComponent.GlobalEventCount);

        gameSystem.DeactivateComponent(childComponent);
        gameSystem.DeactivateComponent(rootComponent);
        gameSystem.DeactivateGameObject(child);
        gameSystem.DeactivateGameObject(root);
        eventHub.Publish(new ProbeEvent());
        eventHub.Drain();

        Assert.Equal(1, root.GlobalEventCount);
        Assert.Equal(1, child.GlobalEventCount);
        Assert.Equal(1, rootComponent.GlobalEventCount);
        Assert.Equal(1, childComponent.GlobalEventCount);
    }

    /// <summary>
    /// Verifies activation callbacks register handlers before publishing activation messages.
    /// </summary>
    [Fact]
    public void ActivateCallbacks_registerHandlersBeforePublishingEvents()
    {
        var eventHub = new EventHub();
        var gameSystem = CreateGameSystem(eventHub);
        var root = new EventHandlingGameObject();
        var component = new EventHandlingComponent();
        var child = new EventHandlingGameObject([component]);
        root.AddChild(child);

        gameSystem.ActivateGameObject(root);
        gameSystem.ActivateGameObject(child);
        gameSystem.ActivateComponent(component);
        Assert.True(component.IsActivated);
        eventHub.Drain();

        Assert.Equal(2, root.GameObjectActivationCount);
        Assert.Equal(2, child.GameObjectActivationCount);
        Assert.Equal(1, component.ComponentActivationCount);

        gameSystem.DeactivateComponent(component);
        gameSystem.DeactivateGameObject(child);
        gameSystem.DeactivateGameObject(root);
        Assert.False(component.IsActivated);
        eventHub.Publish(new ProbeEvent());
        eventHub.Drain();

        Assert.Equal(0, root.GlobalEventCount);
        Assert.Equal(0, child.GlobalEventCount);
        Assert.Equal(0, component.GlobalEventCount);
    }

    /// <summary>
    /// Creates a game system with unused services omitted for lifecycle tests.
    /// </summary>
    /// <param name="eventHub">The event hub to test.</param>
    /// <returns>A game system using the specified event hub.</returns>
    private static GameSystem CreateGameSystem(IEventHub eventHub) =>
        new(eventHub, NullLogger<GameSystem>.Instance);

    /// <summary>
    /// Represents an event used to verify global event dispatch.
    /// </summary>
    private sealed class ProbeEvent : IEvent;

    /// <summary>
    /// Game object that records global and lifecycle event dispatch.
    /// </summary>
    private sealed class EventHandlingGameObject : GameObject
    {
        /// <summary>Initializes an event-handling game object with its components.</summary>
        /// <param name="components">The components owned by this game object.</param>
        public EventHandlingGameObject(IEnumerable<IComponent> components)
            : base(components) { }

        /// <summary>Initializes an event-handling game object without components.</summary>
        public EventHandlingGameObject() { }

        /// <summary>Gets the number of global probe events received.</summary>
        public int GlobalEventCount { get; private set; }

        /// <summary>Gets the number of game-object activation messages received.</summary>
        public int GameObjectActivationCount { get; private set; }

        /// <summary>Handles a global probe event.</summary>
        /// <param name="message">The event message.</param>
        public void Handle(ProbeEvent message) => GlobalEventCount++;

        /// <summary>Handles a game-object activation message.</summary>
        /// <param name="message">The event message.</param>
        public void Handle(GameObjectActivatedEvent message) => GameObjectActivationCount++;
    }

    /// <summary>
    /// Component that records global and lifecycle event dispatch.
    /// </summary>
    private sealed class EventHandlingComponent : Component
    {
        /// <inheritdoc />
        public override string DisplayName => "Event handling component";

        /// <summary>Gets the number of global probe events received.</summary>
        public int GlobalEventCount { get; private set; }

        /// <summary>Gets the number of component activation messages received.</summary>
        public int ComponentActivationCount { get; private set; }

        /// <summary>Handles a global probe event.</summary>
        /// <param name="message">The event message.</param>
        public void Handle(ProbeEvent message) => GlobalEventCount++;

        /// <summary>Handles a component activation message.</summary>
        /// <param name="message">The event message.</param>
        public void Handle(ComponentActivatedEvent message) => ComponentActivationCount++;
    }
}
