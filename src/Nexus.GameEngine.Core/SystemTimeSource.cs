namespace Nexus.GameEngine.Core;

internal sealed class SystemTimeSource : ITimingSource
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);
}
