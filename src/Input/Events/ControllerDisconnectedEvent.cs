namespace Nexus.Input.Events;

/// <summary>Indicates that a controller became unavailable.</summary>
/// <param name="controller">The disconnected controller.</param>
public sealed class ControllerDisconnectedEvent(IController controller) : IEvent
{
    /// <summary>Gets the disconnected controller.</summary>
    public IController Controller { get; } = controller;
}
