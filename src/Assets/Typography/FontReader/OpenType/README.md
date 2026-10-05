# OpenType font reader

`OpenTypeFontReader` is the entry point used by `FontBuilder` for both `.ttf` and
`.otf` files. Outline selection uses SFNT table tags rather than the file extension:

- `glyf` outlines use the existing TrueType decoder and contour converter.
- `CFF ` outlines use the managed CFF 1 and Type 2 decoder in this directory.

Both formats share the existing `head`, `maxp`, `hhea`, `hmtx`, Unicode `cmap`,
GPOS, and legacy `kern` readers. Horizontal advances come from `hmtx`; embedded
CFF widths are consumed only to interpret the charstring stack correctly.

CFF supports name-keyed and CID-keyed fonts, FDSelect formats 0 and 3, local and
global subroutines, stem/hint masks, line/curve/flex operators, and Type 2 stack,
arithmetic, and transient storage operators. Hinting is consumed but not applied
to distance-field geometry. The random operator uses a deterministic value of 0.5.

Cubic curves are subdivided into quadratic segments before entering the existing
MSDF stages. `GetGlyphContours` accepts an optional positive, finite
`cubicApproximationTolerance` in font units (default 0.01). The difference between
the two degree-reduced control points must be at most four times this tolerance,
conservatively bounding the curve approximation error below the tolerance
(apart from floating-point rounding). The default retains the original 0.04
control-point threshold. Fractional charstring coordinates
are preserved. Contours close implicitly at a new moveto or endchar.

Choose tolerances using `pixelError = fontUnitError * emSize / unitsPerEm` and
the largest intended raster size. Exported embedded outlines retain their
subdivision; changing a reader tolerance does not simplify existing embedded
data. NAP export and built-in data retain the original precision. Runtime file
fonts use a 0.006-pixel budget converted to font units at the selected raster
em size; the decoded-outline cache includes that size. Embedded outlines are
coalesced at realization using a separate cumulative 0.006-pixel error budget,
in addition to their original export error. Their original records are preserved.
See [the subdivision investigation](CffSubdivisionPerformance.md) for measurements.

CFF2 variable outlines, Type 1 charstrings, non-default FontMatrix transforms,
and deprecated endchar composite glyphs produce explicit `InvalidDataException`
diagnostics. INDEX ranges, operand counts, subroutine indices, recursion depth,
and execution/geometry budgets are checked before or during decoding.

Format references: Adobe's [CFF specification](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5176.CFF.pdf)
and [Type 2 charstring specification](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5177.Type2.pdf).
