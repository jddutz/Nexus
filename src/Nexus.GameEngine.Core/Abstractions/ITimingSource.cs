namespace Nexus.GameEngine.Core.Abstractions;

public interface ITimingSource
{
    TimeSpan Elapsed { get; }
}
