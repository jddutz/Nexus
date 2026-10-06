namespace Nexus.Core.Events;

/// <summary>
/// Provides registration and dispatch of events.
/// </summary>
public interface IEventHub
{
    /// <summary>
    /// Registers an object containing event handler methods.
    /// </summary>
    /// <param name="handler">The object to register.</param>
    void Register(object handler);

    /// <summary>
    /// Removes an object and its event handler methods from the hub.
    /// </summary>
    /// <param name="handler">The object to unregister.</param>
    void Unregister(object handler);

    /// <summary>
    /// Queues an event for dispatch.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    void Publish(IEvent @event);

    /// <summary>
    /// Dispatches the events currently queued in the hub.
    /// </summary>
    void Drain();

    /// <summary>
    /// Removes all events currently queued in the hub without dispatching them.
    /// </summary>
    void ClearPendingEvents();
}
