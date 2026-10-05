namespace Nexus.Core.Performance;

/// <summary>Times synchronous work only when its collector is enabled; disabled scopes read no clocks.</summary>
public readonly ref struct LoadPerformanceScope
{
    private readonly IPerformanceTelemetry? _telemetry;
    private readonly string _operation;
    private readonly string? _resource;
    private readonly int _size;
    private readonly int _codepoint;
    private readonly long _units;
    private readonly long _start;
    private readonly long _allocated;

    public LoadPerformanceScope(IPerformanceTelemetry? telemetry, string operation,
        string? resource = null, int size = 0, int codepoint = -1, long units = 0)
    {
        _telemetry = telemetry?.IsEnabled == true ? telemetry : null;
        _operation = operation;
        _resource = resource;
        _size = size;
        _codepoint = codepoint;
        _units = units;
        _start = _telemetry is null ? 0 : Stopwatch.GetTimestamp();
        _allocated = _telemetry is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
    }

    public void Dispose()
    {
        if (_telemetry is null) return;
        Dispose(GC.GetAllocatedBytesForCurrentThread() - _allocated);
    }

    /// <summary>Records an explicitly summed allocation count for parallel work.</summary>
    public void Dispose(long allocatedBytes)
    {
        if (_telemetry is null) return;
        _telemetry.RecordDuration(_operation, _resource, Stopwatch.GetElapsedTime(_start).TotalMilliseconds,
            allocatedBytes, _size, _codepoint, _units);
    }
}
