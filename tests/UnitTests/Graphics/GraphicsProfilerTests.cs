using Microsoft.Extensions.Options;
using Nexus.Core;
using Nexus.Core.Performance;
using Nexus.Graphics;

namespace Nexus.UnitTests.Graphics;

public sealed class GraphicsProfilerTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void CollectionRequiresDebuggingOrPerformanceMetrics(bool debugging, bool metrics, bool diagnostics)
    {
        using var profiler = new GraphicsProfiler(Options.Create(new DiagnosticsSettings
        {
            Debugging = debugging, EnablePerformanceMetrics = metrics, EnableDiagnostics = diagnostics,
        }));
        Assert.Equal(debugging || metrics, profiler.IsEnabled);
        profiler.RecordDuration("startup", null, 10, 100);
        profiler.RecordCache("registry", null, true);
        Assert.Equal(debugging || metrics ? 2 : 0, profiler.GetSnapshot().Count);
    }

    [Fact]
    public void AggregatesPreserveWorkAllocationsCacheCountsAndSlowestGlyph()
    {
        using var profiler = new GraphicsProfiler(Options.Create(new DiagnosticsSettings { EnablePerformanceMetrics = true }));
        profiler.RecordDuration("font.msdf.glyph", "font", 4, 100, 16, 65, 10);
        profiler.RecordDuration("font.msdf.glyph", "font", 7, 200, 16, 66, 20);
        var sample = Assert.Single(profiler.GetSnapshot());
        Assert.Equal(2, sample.Count);
        Assert.Equal(11, sample.TotalMs);
        Assert.Equal(7, sample.MaximumMs);
        Assert.Equal(300, sample.AllocatedBytes);
        Assert.Equal(30, sample.Units);
        Assert.Equal(66, sample.SlowestCodepoint);
        profiler.RecordCache("registry", "font", true);
        profiler.RecordCache("registry", "font", false);
        Assert.Equal(3, profiler.GetSnapshot().Count);
        profiler.Reset();
        Assert.Empty(profiler.GetSnapshot());
    }

    [Fact]
    public void ScopeRecordsSynchronousAllocationAndDuration()
    {
        using var profiler = new GraphicsProfiler(Options.Create(new DiagnosticsSettings { Debugging = true }));
        byte[] buffer;
        using (var scope = new LoadPerformanceScope(profiler, "registry.load", units: 1))
            buffer = new byte[4096];
        GC.KeepAlive(buffer);
        var sample = Assert.Single(profiler.GetSnapshot());
        Assert.True(sample.TotalMs >= 0);
        Assert.True(sample.AllocatedBytes >= 4096);
        Assert.Equal(1, sample.Units);
    }
}
