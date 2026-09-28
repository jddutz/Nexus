namespace Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;

/// <summary>
/// Verifies game objects and components participate in global event handling with their lifecycle.
/// </summary>
public class GameSystemEventRegistrationTests
{
    /// <summary>
    /// Verifies model registration wires an object tree and unregistering removes its handlers.
    /// </summary>
    [Fact]
    public void RegisterGameObject_registersItsSubtreeForGlobalEvents()
    {
        var eventHub = new EventHub();
        var gameSystem = CreateGameSystem(eventHub);
        var root = new EventHandlingGameObject();
        var child = new EventHandlingGameObject();
        var rootComponent = new EventHandlingComponent();
        var childComponent = new EventHandlingComponent();
        root.AddComponent(rootComponent);
        root.AddChild(child);
        child.AddComponent(childComponent);

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
        var child = new EventHandlingGameObject();
        var component = new EventHandlingComponent();
        root.AddChild(child);
        child.AddComponent(component);

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
