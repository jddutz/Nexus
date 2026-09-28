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
                new Dictionary<string, string?> { ["Game:InitialScene"] = "ConfiguredScene" }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddGameServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<IOptions<GameSettings>>().Value;

        Assert.Equal("ConfiguredScene", settings.InitialScene);
    }

    /// <summary>
    /// Verifies initialization loads the scene identified by game settings.
    /// </summary>
    [Fact]
    public void Initialize_loadsTheConfiguredInitialScene()
    {
        var sceneId = (SceneId)"WelcomeScreen";
        var scene = new Scene(sceneId);
        var sceneRegistry = new SceneRegistry();
        sceneRegistry.Register(sceneId, () => scene);
        var gameSystem = CreateGameSystem(
            new EventHub(),
            sceneRegistry,
            new GameSettings { InitialScene = "WelcomeScreen" }
        );

        Assert.Null(gameSystem.InitialScene);

        gameSystem.Initialize();

        Assert.Same(scene, gameSystem.InitialScene);
        Assert.Same(scene, gameSystem.CurrentScene);
    }

    /// <summary>
    /// Verifies initialization propagates a failure when the configured scene is absent.
    /// </summary>
    [Fact]
    public void Initialize_throwsWhenConfiguredInitialSceneIsNotRegistered()
    {
        var gameSystem = CreateGameSystem(
            new EventHub(),
            new SceneRegistry(),
            new GameSettings { InitialScene = "MissingScene" }
        );

        var exception = Assert.Throws<InvalidOperationException>(gameSystem.Initialize);

        Assert.Contains("MissingScene", exception.Message);
        Assert.Null(gameSystem.CurrentScene);
    }

    /// <summary>
    /// Verifies model registration wires an object tree and unregistering removes its handlers.
    /// </summary>
    [Fact]
    public void RegisterGameObject_registersItsSubtreeForGlobalEvents()
    {
        var eventHub = new EventHub();
        var gameSystem = CreateGameSystem(eventHub);
        var rootComponent = new EventHandlingComponent();
        var root = new EventHandlingGameObject([rootComponent]);
        var childComponent = new EventHandlingComponent();
        var child = new EventHandlingGameObject([childComponent]);
        root.AddChild(child);

        root.SetGameModel(gameSystem);
        eventHub.Publish(new ProbeEvent());
        eventHub.Drain();

        Assert.Equal(1, root.GlobalEventCount);
        Assert.Equal(1, child.GlobalEventCount);
        Assert.Equal(1, rootComponent.GlobalEventCount);
        Assert.Equal(1, childComponent.GlobalEventCount);

        gameSystem.UnregisterGameObject(root);
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
        gameSystem.ActivateComponent(component);
        Assert.True(component.IsActivated);
        eventHub.Drain();

        Assert.Equal(1, root.GameObjectActivationCount);
        Assert.Equal(1, child.GameObjectActivationCount);
        Assert.Equal(1, component.ComponentActivationCount);

        gameSystem.DeactivateGameObject(root);
        gameSystem.DeactivateComponent(component);
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
    private static GameSystem CreateGameSystem(
        IEventHub eventHub,
        ISceneRegistry? sceneRegistry = null,
        GameSettings? gameSettings = null
    ) =>
        new(
            eventHub,
            NullLogger<GameSystem>.Instance,
            sceneRegistry ?? new SceneRegistry(),
            Options.Create(gameSettings ?? new GameSettings())
        );

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
