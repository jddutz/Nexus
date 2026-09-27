namespace Nexus.Input.Events;

/// <summary>Indicates that a controller became available.</summary>
/// <param name="controller">The connected controller.</param>
public sealed class ControllerConnectedEvent(IController controller) : IEvent
{
    /// <summary>Gets the connected controller.</summary>
    public IController Controller { get; } = controller;
}
