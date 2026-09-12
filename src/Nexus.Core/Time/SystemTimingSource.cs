namespace Nexus.Core;

/// <summary>
/// Provides elapsed time measured from the creation of the timing source.
/// </summary>
public sealed class SystemTimingSource : ITimingSource
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// Gets the elapsed time since the timing source was created.
    /// </summary>
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);
}
