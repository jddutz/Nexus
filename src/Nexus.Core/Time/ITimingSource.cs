namespace Nexus.Core;

public interface ITimingSource
{
    TimeSpan Elapsed { get; }
}
