# CFF subdivision investigation

The existing Type 2 decoder recursively halves cubic curves until the two
degree-reduced quadratic control points differ by at most 0.04 font units.
It uses their average as the quadratic control point. This conservatively
bounds approximation error by 0.01 font units, excluding float rounding.

Aileron Regular uses 1,000 units/em. That bound is 0.00024 pixels at em size 24
and 0.00256 pixels at the engine's maximum em size 256. This precision produces
6,144 segments across printable ASCII: 271 for `@`, 208 for `8`, 157 for `Q`,
155 for `&`, 132 for `%`, and 32 for the dot.

The reader now accepts an explicit tolerance while preserving its default.
Measurements used `.content/fonts/nexus.default.regular.otf`, 95 printable ASCII
glyphs, .NET 10 Release, distance range 4, bitmap padding 6. Outlines were decoded
before timing. One glyph warmed each path. Allocations were measured on the
calling thread. These are single-run observations, not statistical benchmarks;
they are not directly comparable to application startup or Debug timings.

| Tolerance, font units | Segments | `@` | Dot | Raster time at em 24 | Changed RGB bytes | Maximum byte difference |
|---|---:|---:|---:|---:|---:|---:|
| 0.01 | 6,144 | 271 | 32 | 409.64 ms | Baseline | Baseline |
| 0.10 | 3,198 | 139 | 16 | 177.85 ms | 131 | 1 |
| 0.25 | 2,497 | 96 | 16 | 131.91 ms | 403 | 1 |
| 0.50 | 2,186 | 78 | 8 | 118.87 ms | 764 | 89 |
| 1.00 | 1,819 | 69 | 8 | 101.05 ms | 3,959 | 89 |

All five paths produced identical bitmap dimensions. Comparisons contain
178,143 RGB bytes. At tolerance 0.25, the mean absolute byte difference was
0.00226 and cumulative raster allocations were 228,032 bytes versus 231,824.

At em 48, a separate 0.01/0.25 comparison measured 791.17/294.55 ms.
1,968 of 402,630 RGB bytes changed; three glyphs (`J`, `d`, `n`) had individual
channel differences over one byte (maximum 255). However, RGB median differences
were at most one byte: 784 of 134,210 pixel medians changed, with a mean absolute
median difference of 0.00584. MSDF channel changes must be assessed through the
rendered median as well as raw bytes, because geometry subdivision can alter
edge colors and near-boundary winding classifications.

At em 256, the 0.01/0.25 comparison measured 14,409.55/6,578.11 ms. Dimensions
were unchanged, 73,437 of 6,059,592 RGB bytes changed, mean absolute channel
difference was 0.01622, maximum 255. Median and rendered visual comparisons
at this size were not performed. The conservative geometric bound for 0.25
is 0.006 pixels at em 24, 0.012 at em 48, and 0.064 at em 256.

Subdivision is a useful CPU optimization target before GPU rasterization.
0.25 is a promising candidate, but should receive rendered checks across sizes
and font faces before becoming an export/runtime default. Existing embedded
outlines must be regenerated from the OTF to benefit. No default or generated
font data changed in this investigation.

Follow-up implementation: runtime tolerance now scales with raster em size,
and embedded quadratics are coalesced within a cumulative error budget. See
[the current rasterization policy](../../../../Graphics/Text/RasterizationPolicy.md)
for current behavior and measurements. The tables above describe the original
fixed-tolerance investigation.

The local ignored comparison harness is `.tmp/cff-benchmark`; run it from the
repository root with `dotnet run --project .tmp/cff-benchmark/Benchmark.csproj -c Release`.
Its final configuration compares em 48 medians at tolerances 0.01 and 0.25.
Reader tests sample 1,001 points of a synthetic cubic at each supported test
tolerance, check approximation error and contour closure, and reject invalid
tolerances.
