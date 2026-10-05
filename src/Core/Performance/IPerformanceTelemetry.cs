namespace Nexus.Core.Performance;

/// <summary>Collects load-time measurements independently of frame profiling.</summary>
public interface IPerformanceTelemetry
{
    bool IsEnabled { get; }
    void RecordDuration(string operation, string? resource, double elapsedMs,
        long allocatedBytes, int size = 0, int codepoint = -1, long units = 0);
    void RecordCache(string cache, string? resource, bool hit);
    IReadOnlyList<LoadPerformanceSample> GetSnapshot();
    void Reset();
}

/// <summary>Aggregates an operation by resource and generation size. Parent and child timings overlap.</summary>
public sealed record LoadPerformanceSample(string Operation, string? Resource, int Size,
    long Count, double TotalMs, double MaximumMs, long AllocatedBytes, long Units,
    int SlowestCodepoint);
