# Controlled glyph parallelism comparison

Run outside the debugger:

    dotnet run --project tools/FontGenerationBenchmark/FontGenerationBenchmark.csproj -c Release

The program rejects Debug builds and an attached debugger. It uses embedded
Aileron Regular, em 32, exactly 95 printable ASCII glyphs, the current tolerance
and fixed bucket behavior. It performs complete FontBuilder builds, including
reconstruction/simplification, bounds, rasterization, packing, and metadata.
It excludes first-use embedded initialization, texture handling and GPU work.

Each worker setting receives one warm-up, followed by seven measured builds.
Settings rotate between runs to reduce order effects. Output includes median,
minimum and maximum wall time. Limits are 1, 2, 4, 8 and automatic (0).

There are two passes: telemetry disabled, then telemetry enabled with no logger
and no metric listener. Per-glyph logging is disabled in both. The first pass
reads process-wide GC.GetTotalAllocatedBytes(precise: true) before and after each
build, covering all worker threads. This includes scheduling/setup and any other
managed process activity during the interval. The isolated benchmark process
has no application frame/render workload.

The telemetry pass also reports summed per-task glyph allocations from
font.glyphs.build. This includes outline preparation and rasterization on all
workers, but excludes scheduling, packing and result assembly. Its per-glyph
milliseconds overlap and must not be added as batch CPU execution time.
Zero telemetry fields in the disabled pass mean unavailable, not zero actual work.

Observed on 16 logical CPUs, Release, no debugger:

| Workers | Telemetry off wall median | All-thread allocations | Telemetry on wall median | Glyph-task allocations |
|---|---:|---:|---:|---:|
| 1 | 97.60 ms | 1,500,984 B | 87.55 ms | 1,213,216 B |
| 2 | 48.22 ms | 1,502,704 B | 42.38 ms | 1,213,216 B |
| 4 | 27.60 ms | 1,503,200 B | 22.91 ms | 1,213,216 B |
| 8 | 18.87 ms | 1,504,128 B | 15.79 ms | 1,213,216 B |
| Auto (8) | 21.04 ms | 1,504,128 B | 16.46 ms | 1,213,216 B |

The second pass follows additional JIT warm-up and may run faster. These separate
pass medians do not establish negative telemetry overhead. Likewise, this is a
small local benchmark, not a statistical claim about all machines or cold startup.
The 6.91-second application regression does not reproduce under these conditions.
Release parallelism improves the same workload substantially with stable allocations.

Debug compilation, an attached debugger, debug log sinks, cold JIT, concurrent
application activity and machine load remain possible differences in the reported
application run. This benchmark does not isolate which caused that regression.
No worker default, fixed bucket or distance calculation was changed for this comparison.
