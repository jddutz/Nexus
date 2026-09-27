namespace Nexus.Core.Events;

/// <summary>
/// Marks an event that may be omitted from diagnostic logs because it occurs frequently.
/// </summary>
public interface INoisyEvent : IEvent;
