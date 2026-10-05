using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Text;
#if DEBUG
throw new InvalidOperationException("Run this comparison in Release.");
#endif
if(Debugger.IsAttached) throw new InvalidOperationException("Detach the debugger.");
BuiltInFonts.TryGetRasterizerInput(BuiltInFonts.Regular,out var input);
var codes=Enumerable.Range(32,95).ToArray();
Console.WriteLine($"Release; debugger={Debugger.IsAttached}; CPUs={Environment.ProcessorCount}; em=32; glyphs={codes.Length}; no logger; no metric listener.");
foreach(var telemetry in new[]{false,true}) {
    using var profiler=new GraphicsProfiler(Options.Create(new DiagnosticsSettings{EnablePerformanceMetrics=telemetry}));
    var builder=new FontBuilder(profiler);
    var limits=new[]{1,2,4,8,0};
    var results=limits.ToDictionary(n=>n,n=>new List<(double Wall,long Total,long Batch,double GlyphMs)>());
    foreach(var limit in limits)
        builder.Build(BuiltInFonts.Regular,input!,codes,new FontGenerationSettings{EmSize=32,MaxDegreeOfParallelism=limit});
    for(var run=0;run<7;run++) foreach(var limit in limits.Skip(run%limits.Length).Concat(limits.Take(run%limits.Length))) {
        profiler.Reset();
        var before=GC.GetTotalAllocatedBytes(precise:true);
        var start=Stopwatch.GetTimestamp();
        var font=builder.Build(BuiltInFonts.Regular,input!,codes,new FontGenerationSettings{EmSize=32,MaxDegreeOfParallelism=limit});
        var wall=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var total=GC.GetTotalAllocatedBytes(precise:true)-before;
        var samples=profiler.GetSnapshot();
        var batch=samples.SingleOrDefault(s=>s.Operation=="font.glyphs.build");
        var glyph=samples.SingleOrDefault(s=>s.Operation=="font.msdf.glyph");
        results[limit].Add((wall,total,batch?.AllocatedBytes??0,glyph?.TotalMs??0));
        if(font.Glyphs.Count!=95) throw new Exception("Wrong glyph count");
    }
    foreach(var limit in limits) {
        var data=results[limit];
        Console.WriteLine($"telemetry={telemetry} workers={limit} wallMedianMs={data.Select(r=>r.Wall).Order().ElementAt(3):F2} min={data.Min(r=>r.Wall):F2} max={data.Max(r=>r.Wall):F2} processAllocatedMedian={data.Select(r=>r.Total).Order().ElementAt(3)} workerBatchAllocatedMedian={data.Select(r=>r.Batch).Order().ElementAt(3)} summedGlyphMsMedian={data.Select(r=>r.GlyphMs).Order().ElementAt(3):F2}");
    }
}
