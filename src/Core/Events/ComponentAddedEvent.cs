namespace Nexus.Core.Events;

/// <summary>
/// Indicates that a component was added to a game object in the active scene.
/// </summary>
/// <param name="component">The component that was added.</param>
public sealed class ComponentAddedEvent(IComponent component) : IEvent
{
    /// <summary>
    /// Gets the component that was added.
    /// </summary>
    public IComponent Component { get; } = component;
}
