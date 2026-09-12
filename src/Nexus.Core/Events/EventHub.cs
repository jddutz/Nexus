using System.Linq.Expressions;
using System.Reflection;

namespace Nexus.Core.Events;

public sealed class EventHub : IEventHub
{
    private const string HANDLE_METHOD_NAME = "Handle";

    private readonly HashSet<object> _registeredHandlers = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Type, List<EventSubscription>> _subscriptions = [];
    private readonly ConcurrentQueue<IEvent> _events = new();

    private static bool IsEventHandler(MethodInfo m)
    {
        if (m.Name != HANDLE_METHOD_NAME)
            return false;

        var p = m.GetParameters();

        return p.Length == 1 && p[0].ParameterType.IsAssignableTo(typeof(IEvent));
    }

    private static Action<IEvent> Compile(object handler, MethodInfo method, Type eventType)
    {
        var eventParameter = Expression.Parameter(typeof(IEvent), "event");

        var target = Expression.Constant(handler);
        var castEvent = Expression.Convert(eventParameter, eventType);

        var call = Expression.Call(target, method, castEvent);

        return Expression.Lambda<Action<IEvent>>(call, eventParameter).Compile();
    }

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

    public void Publish(IEvent @event)
    {
        _events.Enqueue(@event);
    }

    public void Drain()
    {
        var count = _events.Count;

        for (var i = 0; i < count; i++)
        {
            if (_events.TryDequeue(out var @event) && @event is not null)
            {
                if (!_subscriptions.TryGetValue(@event.GetType(), out var eventSubs))
                    continue;

                foreach (var sub in eventSubs)
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

    private sealed class EventSubscription
    {
        public required object Handler { get; init; }
        public required Action<IEvent> Action { get; init; }
    }
}
