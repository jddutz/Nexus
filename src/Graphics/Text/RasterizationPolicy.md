# Text rasterization policy

Layout uses the exact requested style size. Atlas resolution is selected
independently of cache history: requests up to 32 use em 32, followed by upward
buckets 48, 64, 96, 128, 192, and 256. Larger requests use the engine's maximum
em 256. Thus 16, 20, 24, and 32 share one atlas, while 48 and 16 always realize
separate buckets regardless of request order. The minimum resolution gives small
text more distance-field samples; pixel alignment and font hinting still affect
small-size appearance, and MSDF rendering does not apply font hinting.

Runtime file-based CFF subdivision uses:

    toleranceInFontUnits = 0.006 * unitsPerEm / rasterEmSize

For a 1,000-unit font this is 0.1875 at em 32, 0.125 at em 48, and 0.0234375
at em 256. The cache keys decoded source outlines by codepoint and raster size
so a larger raster cannot inherit a small-size approximation. TrueType source
outlines are decoded unchanged.

Embedded outlines have already been subdivided at export. At realization,
adjacent quadratic segments may be coalesced. A candidate is split back into
two quadratics and its control polygons compared with both inputs; the convex
hull bounds the entire error curve. Each merge carries previous error forward,
and cumulative additional error cannot exceed the same 0.006-pixel budget.
Endpoints, order, lines and original embedded records are preserved. This
budget is additional to the original export approximation, excludes float
rounding, and is a geometric bound rather than a guarantee of identical MSDF
channel bytes or edge colors.

The MSDF generator encodes distances with denominator 2 * DistanceRange.
The Vulkan fragment shader now uses that full span when reconstructing pixel
coverage. Previously it used only DistanceRange, doubling the antialiasing
transition width except where the shader's minimum-width clamp applied.
The shader source and shipped SPIR-V have both been updated.

Automated tests cover pixel-space tolerance, curve error, closure, exact layout
sizes, and cache selection independent of request order. Rendered application
comparisons are still needed to evaluate the resulting small-text appearance.

Single-run Release comparisons of all 95 Aileron Regular glyphs, starting with
the original 0.01-font-unit outlines, distance range 4 and bitmap padding 6:

| Raster em | Original segments | Coalesced segments | Original time | Coalesced time |
|---|---:|---:|---:|---:|
| 32 | 6,144 | 2,533 | 512.25 ms | 176.68 ms |
| 64 | 6,144 | 3,096 | 953.22 ms | 515.06 ms |

Dimensions matched for every glyph. Maximum RGB median difference was one
byte at both sizes. Individual channels changed more for a few glyphs; median
comparison matters for MSDF rendering. Measurements exclude decoding,
coalescing, atlas packing and GPU work. More segments are retained at larger
sizes to preserve the same pixel-space budget.
