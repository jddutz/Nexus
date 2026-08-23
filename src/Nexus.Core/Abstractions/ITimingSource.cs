namespace Nexus.Core.Abstractions;

public interface ITimingSource
{
    TimeSpan Elapsed { get; }
}
