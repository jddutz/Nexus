namespace Nexus.Core.Abstractions;

public interface IGameClock
{
    TimeSpan Elapsed { get; }
    TimeSpan Delta { get; }
    ulong Tick { get; }

    float TimeScale { get; }
    bool IsPaused { get; }
}
