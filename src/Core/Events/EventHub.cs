using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Nexus.Core.Events;

/// <summary>
/// Provides registration, diagnostic logging, and dispatch of events.
/// </summary>
public sealed class EventHub : IEventHub
{
    private const string HANDLE_METHOD_NAME = "Handle";
    private const int DEFAULT_EVENT_LOG_LIMIT = 20;
    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new Vector2JsonConverter() },
    };

    private readonly ILogger<EventHub>? _logger;
    private readonly bool _diagnosticsEnabled;
    private readonly bool _logHighFrequencyEvents;
    private readonly int _eventLogLimit;
    private readonly HashSet<object> _registeredHandlers = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Type, List<EventSubscription>> _subscriptions = [];
    private readonly Dictionary<Type, int> _eventLogCounts = [];
    private readonly ConcurrentQueue<IEvent> _events = new();

    /// <summary>
    /// Initializes an event hub with an optional logger and diagnostic logging settings.
    /// </summary>
    /// <param name="logger">The logger used to record event type and JSON payload data.</param>
    /// <param name="diagnosticsEnabled">Whether event diagnostic logging is enabled.</param>
    /// <param name="logHighFrequencyEvents">Whether automatic high-frequency event log suppression is bypassed.</param>
    /// <param name="eventLogLimit">The number of event payloads logged per event type before suppression.</param>
    public EventHub(
        ILogger<EventHub>? logger = null,
        bool diagnosticsEnabled = false,
        bool logHighFrequencyEvents = false,
        int eventLogLimit = DEFAULT_EVENT_LOG_LIMIT
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventLogLimit);

        _logger = logger;
        _diagnosticsEnabled = diagnosticsEnabled;
        _logHighFrequencyEvents = logHighFrequencyEvents;
        _eventLogLimit = eventLogLimit;
    }

    /// <summary>
    /// Determines whether a method handles one event type.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns><see langword="true"/> when the method is a single-event handler.</returns>
    private static bool IsEventHandler(MethodInfo m)
    {
        if (m.Name != HANDLE_METHOD_NAME)
            return false;

        var p = m.GetParameters();

        return p.Length == 1 && p[0].ParameterType.IsAssignableTo(typeof(IEvent));
    }

    /// <summary>
    /// Compiles an event handler into a delegate accepting the event interface.
    /// </summary>
    /// <param name="handler">The target object.</param>
    /// <param name="method">The handler method.</param>
    /// <param name="eventType">The event type accepted by the handler.</param>
    /// <returns>A delegate that invokes the handler with an event.</returns>
    private static Action<IEvent> Compile(object handler, MethodInfo method, Type eventType)
    {
        var eventParameter = Expression.Parameter(typeof(IEvent), "event");

        var target = Expression.Constant(handler);
        var castEvent = Expression.Convert(eventParameter, eventType);

        var call = Expression.Call(target, method, castEvent);

        return Expression.Lambda<Action<IEvent>>(call, eventParameter).Compile();
    }

    /// <summary>
    /// Registers an object containing event handler methods.
    /// </summary>
    /// <param name="handler">The object to register.</param>
    public void Register(object handler)
    {
        if (handler is null || _registeredHandlers.Contains(handler))
            return;

        var handlerMethods = handler.GetType().GetMethods().Where(IsEventHandler);

        foreach (var method in handlerMethods)
        {
            var eventType = method.GetParameters()[0].ParameterType;

            var sub = new EventSubscription
            {
                Handler = handler,
                Action = Compile(handler, method, eventType),
            };

            if (_subscriptions.TryGetValue(eventType, out var eventSubs))
            {
                eventSubs.Add(sub);
            }
            else
            {
                _subscriptions.Add(eventType, [sub]);
            }
        }

        _registeredHandlers.Add(handler);
    }

    /// <summary>
    /// Removes an object and its event handler methods from the hub.
    /// </summary>
    /// <param name="handler">The object to unregister.</param>
    public void Unregister(object handler)
    {
        if (handler is null)
            return;

        var handlerMethods = handler.GetType().GetMethods().Where(IsEventHandler);

        foreach (var method in handlerMethods)
        {
            var eventType = method.GetParameters()[0].ParameterType;

            if (_subscriptions.TryGetValue(eventType, out var eventSubs))
            {
                eventSubs.RemoveAll(sub => sub.Handler == handler);
            }
        }

        _registeredHandlers.Remove(handler);
    }

    /// <summary>
    /// Queues an event for dispatch.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    public void Publish(IEvent @event)
    {
        _events.Enqueue(@event);
    }

    /// <summary>
    /// Logs and dispatches the events currently queued in the hub.
    /// </summary>
    public void Drain()
    {
        var count = _events.Count;

        for (var i = 0; i < count; i++)
        {
            if (_events.TryDequeue(out var @event) && @event is not null)
            {
                LogEvent(@event);

                if (!_subscriptions.TryGetValue(@event.GetType(), out var eventSubs))
                    continue;

                foreach (var sub in eventSubs.ToArray())
                {
                    try
                    {
                        sub.Action(@event);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"Unhandled error in {sub.Handler.GetType().FullName} "
                                + $"while handling {@event.GetType().FullName}."
                        );

                        Console.Error.WriteLine(ex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Removes all events currently queued in the hub without dispatching them.
    /// </summary>
    public void ClearPendingEvents()
    {
        while (_events.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Writes the event type and cycle-safe JSON payload to the configured logger.
    /// </summary>
    /// <param name="event">The event being dispatched.</param>
    private void LogEvent(IEvent @event)
    {
        if (!_diagnosticsEnabled || _logger is null || !_logger.IsEnabled(LogLevel.Information))
            return;

        var eventType = @event.GetType();
        if (!_logHighFrequencyEvents && !ShouldLogEvent(eventType))
            return;

        string eventJson;
        try
        {
            eventJson = JsonSerializer.Serialize(@event, eventType, EventJsonOptions);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not serialize event {EventType} for diagnostic logging.",
                eventType.FullName
            );
            eventJson = "{}";
        }

        _logger.LogInformation(
            "Dispatching event {EventType}: {EventDataJson}",
            eventType.FullName,
            eventJson
        );
    }

    /// <summary>
    /// Determines whether diagnostic logging for an event type remains below the event-count limit.
    /// </summary>
    /// <param name="eventType">The concrete event type used as the event identifier.</param>
    /// <returns><see langword="true"/> when this event should be logged.</returns>
    private bool ShouldLogEvent(Type eventType)
    {
        _eventLogCounts.TryGetValue(eventType, out var eventCount);
        if (eventCount > _eventLogLimit)
            return false;

        eventCount++;
        _eventLogCounts[eventType] = eventCount;

        if (eventCount <= _eventLogLimit)
            return true;

        _logger!.LogInformation(
            "Suppressing diagnostic logging for event {EventType} after {EventCount} events.",
            eventType.FullName ?? eventType.Name,
            eventCount
        );
        return false;
    }

    /// <summary>
    /// Serializes a vector using explicit coordinate fields instead of derived length properties.
    /// </summary>
    private sealed class Vector2JsonConverter : JsonConverter<Vector2D<float>>
    {
        /// <summary>
        /// Reads a vector from its X and Y coordinates.
        /// </summary>
        /// <param name="reader">The JSON reader positioned at the vector object.</param>
        /// <param name="typeToConvert">The vector type.</param>
        /// <param name="options">The serializer options.</param>
        /// <returns>The vector represented by the JSON object.</returns>
        public override Vector2D<float> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            return new Vector2D<float>(
                root.GetProperty("X").GetSingle(),
                root.GetProperty("Y").GetSingle()
            );
        }

        /// <summary>
        /// Writes a vector as explicit X and Y coordinates.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The vector to write.</param>
        /// <param name="options">The serializer options.</param>
        public override void Write(
            Utf8JsonWriter writer,
            Vector2D<float> value,
            JsonSerializerOptions options
        )
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Stores one registered handler and its compiled dispatch delegate.
    /// </summary>
    private sealed class EventSubscription
    {
        /// <summary>
        /// Gets the registered handler instance.
        /// </summary>
        public required object Handler { get; init; }

        /// <summary>
        /// Gets the compiled event dispatch delegate.
        /// </summary>
        public required Action<IEvent> Action { get; init; }
    }
}
