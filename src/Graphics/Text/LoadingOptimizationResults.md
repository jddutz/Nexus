# Loading optimization comparison

The fixed raster buckets remain 32, 48, 64, 96, 128, 192 and 256. No atlas-sharing
policy, tolerance budget, glyph order or shader behavior changed in this work.

MSDF generation now precomputes a conservative control-polygon bounding box for
each edge. A pixel skips a closest-point solve only if its lower-bound distance
cannot improve the shape distance or any channel assigned to that edge. Winding
classification still visits every edge. Bounds and comparisons include margins
for floating-point rounding; the original solver and evaluation order remain in
use for all surviving candidates.

Release comparison, .NET 10, embedded Aileron Regular, all 95 printable ASCII
glyphs, distance range 4, bitmap padding 6, telemetry and logging disabled:

| Raster em | Original raster median | Pruned raster median |
|---|---:|---:|
| 32 | 192.16 ms | 111.84 ms |
| 48 | 326.91 ms | 146.64 ms |

Each path warmed with one glyph, then three full batches were measured and their
median reported. Outline decoding/coalescing, atlas packing and GPU work are
excluded. These isolated timings are not application time to first frame.
They show roughly 1.7x and 2.2x improvements at the same sizes. The local harness
is .tmp/load-benchmark; run from the repository root with:

    dotnet run --project .tmp/load-benchmark/Benchmark.csproj -c Release

A separate corpus comparison checked all 16 built-in faces at both sizes:
3,040 glyph bitmaps retained every dimension and RGB byte. Regression tests
retain the original solver's packed atlas SHA256 checksums for Regular at both
sizes, alongside existing synthetic geometry tests.

Texture identity hashing now feeds IEEE floating-point bits directly to the
existing little-endian integer hash path. It avoids a byte array per channel
while retaining the same fingerprint, including signed zero and NaN bits.
Texture serialization uses the existing span-writing color API for all formats,
with a direct RGBA8 loop for the common upload formats. The conversion, rounding,
channel order and range validation are preserved.

For a 512x512 RGBA8 texture, single warmed observations measured:

| Stage | Original time | Optimized time | Original allocations | Optimized allocations |
|---|---:|---:|---:|---:|
| Identity hash | 10.58 ms | 9.09 ms | 33,554,528 B | 160 B |
| Serialization | 2.35 ms | 1.18 ms | 8,388,648 B | 40 B |

Serialization measurement includes 40 bytes for the benchmark stopwatch. The
serialization implementation itself allocates zero bytes with a supplied target.
Tests verify fingerprint compatibility, every supported format, nonzero source
offsets and absence of per-pixel allocations. The main expected startup saving
comes from rasterization; hashing/serialization chiefly remove allocation pressure.
Application time to first frame still needs measurement in the actual runtime.

Validation: all 419 unit tests passed in Release; git diff whitespace checks passed.

## Parallel glyph generation

The subsequent implementation prepares all outlines and bounds on the caller,
then dispatches independent glyph rasters through a bounded Parallel.For. Every
task writes one preassigned result slot, so atlas packing, glyph metadata and
requested codepoint order remain deterministic. Packing and metadata assembly
remain sequential. Raster failures join active workers and throw FontBuildException
with the underlying aggregate exception; no incomplete atlas is returned.

FontGenerationSettings.MaxDegreeOfParallelism defaults to zero (automatic):
max(1, min(logical processor count - 1, 8)). A positive value explicitly limits
workers; one selects the sequential path. Batches smaller than eight glyphs also
use the sequential path. Negative values are rejected. This applies to both file
and embedded input builds, including runtime text styles through default settings.

Release, 16 logical processors, telemetry/logging disabled, all 95 embedded
Regular glyphs, one complete warm-up build and median of three complete builds:

| Workers | Em 32 | Em 48 |
|---|---:|---:|
| 1 | 129.32 ms | 145.86 ms |
| 2 | 67.13 ms | 78.67 ms |
| 4 | 26.90 ms | 40.88 ms |
| 8 | 19.12 ms | 28.69 ms |
| Auto (8) | 19.05 ms | 26.65 ms |

These timings include outline reconstruction/coalescing, bounds, rasterization,
packing and font metadata. They exclude first-use embedded initialization,
texture construction, GPU uploads and frame presentation. Hardware, tiered JIT
and background load affect scaling; these measurements do not promise the same
startup speedup on every machine. The local .tmp/load-benchmark harness now runs
this worker comparison rather than the earlier pruning/texture comparison.

The batch telemetry reports summed worker allocations and wall time. Per-glyph
durations overlap; other parent allocation scopes remain caller-thread only.
Tests compare atlas bytes, metadata and reversed codepoint order at worker limits
1, 2, 4 and 8, retain reference atlas checksums, validate the worker setting, and
check that batch allocations include the worker samples. All 425 unit tests passed
in Release after this change.

### Parallel preparation follow-up

Glyph tasks now also reconstruct embedded outlines, simplify them and calculate
bounds. The staging array and sequential preparation pass were removed. File
reader/cache access remains synchronized, with a separate lock per cached font
source instead of the builder-wide cache lock. Thus different font files no longer
serialize each other's glyph decoding. Shared reader initialization and cache
mutation for one source remain protected; bounds and rasterization run outside
that lock. Embedded initialization still occurs once before worker dispatch.

Worker allocation totals now include all glyph preparation as well as rasterization.
Order, metadata and reference atlas checksums are unchanged. A fresh three-build
median on the same 16-logical-CPU host measured automatic builds at 19.51 ms
(em 32) and 28.99 ms (em 48), versus sequential builds at 108.07 and 164.16 ms.
These warm timings are similar to the previous parallel-raster-only measurements;
they do not establish an additional speedup from parallel preparation. The
follow-up removes serialization of independent work and improves concurrency
across font sources, while actual cold startup still needs runtime measurement.
