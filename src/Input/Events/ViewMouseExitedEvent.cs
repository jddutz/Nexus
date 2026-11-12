namespace Nexus.Input.Events;

/// <summary>Reports a mouse leaving a view's interactive region without ending its interaction.</summary>
/// <param name="inputEvent">The movement or last known pointer position when the region became unavailable.</param>
public sealed class ViewMouseExitedEvent(MouseMovedEvent inputEvent) : IEvent
{
    /// <summary>Gets the movement or last known pointer position associated with the exit.</summary>
    public MouseMovedEvent InputEvent { get; } = inputEvent;
}
