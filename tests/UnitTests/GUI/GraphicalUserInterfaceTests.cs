using Nexus.Core.Events;
using Nexus.Game;
using Nexus.GUI;

namespace Tests;

/// <summary>
/// Tests GUI element registration and layout invalidation from game lifecycle events.
/// </summary>
public class GraphicalUserInterfaceTests
{
    /// <summary>
    /// Verifies scene discovery and activation/deactivation events update the affected layout.
    /// </summary>
    [Fact]
    public void LifecycleEvents_discoverRegisterAndRemoveActiveElements()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var layoutCount = 0;
        var childArrangementCount = 0;
        var root = new Element(
            arrange: (element, bounds) =>
            {
                layoutCount++;
                ArrangementRules.Default(element, bounds);
            }
        );
        var initiallyDiscoveredChildArrangementCount = 0;
        var initiallyDiscoveredChild = new Element(
            arrange: (_, _) => initiallyDiscoveredChildArrangementCount++
        );
        root.AddChild(initiallyDiscoveredChild);
        var scene = new Scene();
        scene.AddChild(root);
        scene.Activate();
        gui.Initialize();

        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(1, layoutCount);
        Assert.Equal(1, initiallyDiscoveredChildArrangementCount);

        Assert.True(root.RemoveChild(initiallyDiscoveredChild));
        eventHub.Publish(new GameObjectDeactivatedEvent(initiallyDiscoveredChild));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(2, layoutCount);
        Assert.Equal(1, initiallyDiscoveredChildArrangementCount);

        var child = new Element(arrange: (_, _) => childArrangementCount++);
        root.AddChild(child);
        eventHub.Publish(new GameObjectActivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(3, layoutCount);
        Assert.Equal(1, childArrangementCount);

        Assert.True(root.RemoveChild(child));
        eventHub.Publish(new GameObjectDeactivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(4, layoutCount);
        Assert.Equal(1, childArrangementCount);

        eventHub.Publish(new GameObjectActivatedEvent(child));
        eventHub.Drain();
        gui.Update(0);

        Assert.Equal(4, layoutCount);
        Assert.Equal(1, childArrangementCount);
    }
}
