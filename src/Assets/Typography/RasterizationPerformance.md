# Font rasterization allocation comparison

Release, .NET 10.0.400, embedded Aileron Regular, all 95 printable ASCII
glyphs, em sizes 16 and 48, distance range 4, bitmap padding 6.
Decoded contours were prepared before measurement. Each path was warmed with
one glyph. GC.GetAllocatedBytesForCurrentThread measures cumulative managed
allocations within each complete 95-glyph rasterization batch, excluding font
decoding and atlas packing. Timings are single-run observations, not statistical
benchmark estimates.

| Em size | Original time | Optimized time | Original allocations | Optimized allocations |
|---|---:|---:|---:|---:|
| 16 | 1608.09 ms | 311.00 ms | 5,422,795,672 B | 176,680 B |
| 48 | 5301.78 ms | 643.42 ms | 18,778,578,992 B | 452,512 B |

All 95 bitmap dimensions and every RGB byte matched the original implementation
at both sizes. The local comparison harness is in .tmp/font-benchmark
(ignored scratch files), with copies of the original solver and generator.
Run: dotnet run --project .tmp/font-benchmark/Benchmark.csproj -c Release

The original quadratic solver allocated coefficient arrays, candidate lists,
root arrays, LINQ iterators, sorting storage, and root-deduplication closures
per pixel/edge solve. Bounded stack spans now replace those objects.
The generator shares closest-point results between channel distances and
shape distances, removing the second closest-point solve. Winding traversal
uses indexed edges to avoid allocating enumerators per pixel.

The allocation regression test checks a curved glyph's rasterization allocation
budget against its bitmap payload plus 4096 bytes of setup allowance.
All 16 focused geometry, MSDF, and FontBuilder tests passed in Release.
GPU rasterization remains a separate future engine feature.
