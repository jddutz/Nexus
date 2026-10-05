using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Nexus.Core.Performance;

namespace Nexus.Graphics;

/// <summary>Collects bounded startup aggregates, structured logs, and standard .NET metrics.</summary>
public sealed class GraphicsProfiler : IGraphicsProfiler, IDisposable
{
    private readonly ILogger<GraphicsProfiler>? _logger;
    private readonly object _lock = new();
    private readonly Dictionary<(string Operation, string? Resource, int Size), LoadPerformanceSample> _samples = [];
    private readonly Meter? _meter;
    private readonly Histogram<double>? _duration;
    private readonly Counter<long>? _allocations;
    private readonly Counter<long>? _cache;
    private readonly Counter<long>? _work;
    public bool IsEnabled { get; }

    public GraphicsProfiler(IOptions<DiagnosticsSettings> options, ILogger<GraphicsProfiler>? logger = null)
    {
        IsEnabled = options.Value.PerformanceInstrumentationEnabled;
        _logger = logger;
        if (!IsEnabled) return;
        _meter = new Meter("Nexus.Graphics.Loading", "1.0.0");
        _duration = _meter.CreateHistogram<double>("nexus.graphics.load.duration", "ms");
        _allocations = _meter.CreateCounter<long>("nexus.graphics.load.allocated", "By");
        _work = _meter.CreateCounter<long>("nexus.graphics.load.work", "{unit}");
        _cache = _meter.CreateCounter<long>("nexus.graphics.cache.requests", "{request}");
    }

    public void RecordDuration(string operation, string? resource, double elapsedMs,
        long allocatedBytes, int size = 0, int codepoint = -1, long units = 0)
    {
        if (!IsEnabled) return;
        lock (_lock)
        {
            var key = (operation, resource, size);
            if (_samples.TryGetValue(key, out var previous))
                _samples[key] = previous with
                {
                    Count = previous.Count + 1,
                    TotalMs = previous.TotalMs + elapsedMs,
                    MaximumMs = Math.Max(previous.MaximumMs, elapsedMs),
                    AllocatedBytes = previous.AllocatedBytes + allocatedBytes,
                    Units = previous.Units + units,
                    SlowestCodepoint = elapsedMs > previous.MaximumMs ? codepoint : previous.SlowestCodepoint,
                };
            else if (_samples.Count < 2048)
                _samples[key] = new(operation, resource, size, 1, elapsedMs, elapsedMs, allocatedBytes, units, codepoint);
        }
        // Glyph codepoints stay in logs and aggregates rather than high-cardinality metric tags.
        var tags = new TagList { { "operation", operation }, { "resource", resource }, { "em_size", size } };
        _duration!.Record(elapsedMs, tags);
        _allocations!.Add(allocatedBytes, tags);
        if (units > 0) _work!.Add(units, tags);
        var level = codepoint >= 0 || operation.EndsWith(".hit", StringComparison.Ordinal) || operation.EndsWith(".miss", StringComparison.Ordinal)
            ? LogLevel.Debug : LogLevel.Information;
        if (_logger?.IsEnabled(level) == true)
            _logger.Log(level, "Graphics load {Operation}: Resource={Resource}, EmSize={EmSize}, Codepoint={Codepoint}, ElapsedMs={ElapsedMs:F3}, AllocatedBytes={AllocatedBytes}, Units={Units}",
                operation, resource, size, codepoint, elapsedMs, allocatedBytes, units);
    }

    public void RecordCache(string cache, string? resource, bool hit)
    {
        if (!IsEnabled) return;
        var outcome = hit ? "hit" : "miss";
        _cache!.Add(1, new TagList { { "cache", cache }, { "resource", resource }, { "outcome", outcome } });
        RecordDuration(hit ? cache + ".hit" : cache + ".miss", resource, 0, 0);
    }

    public IReadOnlyList<LoadPerformanceSample> GetSnapshot()
    {
        if (!IsEnabled) return [];
        lock (_lock) return _samples.Values.OrderByDescending(sample => sample.TotalMs).ToArray();
    }

    public void Reset() { if (!IsEnabled) return; lock (_lock) _samples.Clear(); }
    public void Dispose() => _meter?.Dispose();
}
