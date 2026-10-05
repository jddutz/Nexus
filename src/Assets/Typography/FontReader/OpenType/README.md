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
MSDF stages. The difference between the two degree-reduced control points must
be at most 0.04 font units, bounding the curve approximation error below 0.01
font units (apart from floating-point rounding). Fractional charstring coordinates
are preserved. Contours close implicitly at a new moveto or endchar.

CFF2 variable outlines, Type 1 charstrings, non-default FontMatrix transforms,
and deprecated endchar composite glyphs produce explicit `InvalidDataException`
diagnostics. INDEX ranges, operand counts, subroutine indices, recursion depth,
and execution/geometry budgets are checked before or during decoding.

Format references: Adobe's [CFF specification](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5176.CFF.pdf)
and [Type 2 charstring specification](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5177.Type2.pdf).
