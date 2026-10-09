using System.Runtime.CompilerServices;

namespace Nexus.Input;

/// <summary>Temporarily restricts an event hub's input maps, including maps registered later.</summary>
public sealed class InputScope : IDisposable
{
    private sealed class HubState
    {
        public readonly HashSet<InputMap> Maps = [];
        public readonly List<InputScope> Scopes = [];
    }

    private static readonly ConditionalWeakTable<IEventHub, HubState> States = new();
    private readonly HubState _state;
    private readonly Func<InputMap, bool> _accepts;

    public InputScope(IEventHub eventHub, Func<InputMap, bool> accepts)
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(accepts);
        _state = States.GetOrCreateValue(eventHub);
        _accepts = accepts;
        foreach (var map in _state.Maps)
            map.CancelPointerCapture();
        _state.Scopes.Add(this);
    }

    internal static void Register(IEventHub hub, InputMap map) => States.GetOrCreateValue(hub).Maps.Add(map);
    internal static void Unregister(IEventHub hub, InputMap map)
    {
        if (States.TryGetValue(hub, out var state)) state.Maps.Remove(map);
    }
    internal static bool Allows(IEventHub? hub, InputMap map) =>
        hub is null || !States.TryGetValue(hub, out var state)
        || state.Scopes.All(scope => scope._accepts(map));

    public void Dispose()
    {
        if (!_state.Scopes.Remove(this)) return;
        foreach (var map in _state.Maps)
            map.CancelPointerCapture();
    }
}
