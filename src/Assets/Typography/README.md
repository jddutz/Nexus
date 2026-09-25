## Runtime Typography — Technical Specification

### 1. Purpose

`Nexus.Assets/Typography` provides managed font-reading, geometry, distance-field, and atlas-building stages for runtime text-style realization.

Typography is a font-processing subsystem used when the runtime realizes a requested text style. It is not part of NAP's asset compilation step.

Its responsibility is:

```text
Manifest Font File (.ttf/.otf)
    ↓
Read font structures
    ↓
Resolve requested characters
    ↓
Extract glyph metrics and outlines
    ↓
Generate distance-field glyph images
    ↓
Pack glyphs into an atlas
    ↓
Produce runtime glyph data + atlas for the realized style
```

NAP copies the source font unchanged and records only its file path in the content manifest. At runtime, style realization supplies the requested glyphs and rasterization settings; the runtime reads the font and generates the glyph data and atlas it needs. The source font remains the asset, not a pre-generated atlas.

The generated runtime data includes font metrics, glyph advances and bounds, atlas bounds, and kerning required by the `ITextStyle`/`TextSpan` path.

---

## 2. Project location

Typography remains part of the Asset Pipeline project:

```text
src/
└── Assets/
    ├── Fonts/
    ├── Typography/
    │   ├── FontReader/
    │   ├── Geometry/
    │   ├── DistanceFields/
    │   ├── Atlas/
    │   └── ...
    │
    └── ...
```

The font-processing stages must be available to the runtime. They must not be invoked by NAP to preselect glyphs or bake an atlas.

NAP remains responsible only for copying the source font and recording its content path.

---

## 3. Architectural boundary

The subsystem owns the complete transformation from font-file bytes to generated Nexus font data.

```text
                 Nexus.Runtime
                       │
        Text-style realization
                       │
                       ▼
                 Typography
        ┌──────────────┼───────────────┐
        │              │               │
     Reader         Geometry        Generation
        │              │               │
        └──────────────┴───────────────┘
                       │
                       ▼
                FontBuildResult
```

The runtime text-style realization path resolves the manifest font path, determines the glyph repertoire and generation settings needed by the requested style, and invokes the typography stages. NAP does not call `IFontBuilder` or decide the runtime repertoire.

Typography must not know about:

```text
YAML
ContentId
content-manifest.json
asset output directories
Vulkan
IDrawable
TextSpan
```

Those belong outside this subsystem.

---

## 4. Proposed folder structure

I would start deliberately small:

```text
src/AssetPipeline/Typography/
│
├── FontReader/
│   ├── FontReader.cs
│   ├── FontFace.cs
│   ├── FontGlyph.cs
│   ├── FontContour.cs
│   ├── FontPoint.cs
│   │
│   └── TrueType/
│       ├── TrueTypeFontReader.cs
│       ├── TrueTypeReader.cs
│       └── Tables/
│           ├── TableDirectory.cs
│           ├── CmapTable.cs
│           ├── HeadTable.cs
│           ├── HheaTable.cs
│           ├── HmtxTable.cs
│           ├── MaxpTable.cs
│           ├── LocaTable.cs
│           ├── GlyfTable.cs
│           ├── Kern/
│           │   └── KernTable.cs
│           └── Gpos/
│               ├── GposTable.cs
│               └── PairPositioning.cs
│
├── Geometry/
│   ├── Contour.cs
│   ├── Edge.cs
│   ├── LineSegment.cs
│   └── QuadraticSegment.cs
│
├── DistanceFields/
│   ├── MsdfGenerator.cs
│   ├── EdgeColoring.cs
│   └── SignedDistance.cs
│
├── Atlas/
│   ├── FontAtlasBuilder.cs
│   ├── GlyphBitmap.cs
│   └── AtlasPlacement.cs
│
└── TypographyProcessor.cs
```

I would **not consider those exact classes mandatory**. The folder boundaries are more important than prematurely deciding every type.

---

## 5. Processing stages

### FontReader

`FontReader` transforms a supported font file into Nexus-owned font-domain data.

```text
Stream / ReadOnlyMemory<byte>
          ↓
      FontReader
          ↓
       FontFace
```

Conceptually:

```csharp
FontFace Read(Stream source);
```

`FontFace` represents the information extracted from the source font, not the source file itself.

It needs to provide enough information to obtain:

```text
font metrics
codepoint → glyph
glyph advance
glyph outline
kerning
```

It does not rasterize anything.

---

### TrueType reader

The initial implementation supports the subset of TrueType/OpenType required by runtime text realization.

Its parser must operate from the published binary format rather than reproduce the architecture of an existing font library.

The likely table dependency is:

```text
SFNT
 ├─ cmap       character mapping
 ├─ head       units per em / global data
 ├─ hhea       horizontal metrics
 ├─ hmtx       glyph advances
 ├─ maxp       glyph count
 ├─ loca       glyph locations
 ├─ glyf       outlines
 ├─ kern       legacy pair adjustments
 └─ GPOS       OpenType pair positioning
```

That table list should be validated when we write the detailed TrueType-reader spec rather than treated as the final compatibility promise.

The current cmap reader supports Unicode format 4 and format 12. This scope was checked against
the local `.assets/Fonts` reference set: every font has a Unicode format 4 map, and the two
HomeVideo fonts also include Unicode format 12 maps. Format 12 is preferred when available, with
format 4 used for codepoints it does not map and for fonts without format 12. Formats 0 and 6 also
occur in the fixtures but are not needed for their Unicode mappings. Missing or invalid Unicode
codepoints resolve to glyph zero; glyph indices use the SFNT `ushort` range.

Unsupported font features must produce an explicit diagnostic rather than silently generating incorrect assets.

---

## 6. Font-domain geometry

The reader should preserve the source font's geometry without knowing anything about MSDF generation.

For TrueType, the useful intermediate representation is approximately:

```text
FontGlyph
    GlyphIndex
    Advance
    Contours[]

FontContour
    Points[]

FontPoint
    X
    Y
    OnCurve
```

This preserves the native quadratic-outline representation.

The reader should not produce GPU vertices, texture coordinates, pixels, or runtime transformations.

---

## 7. Geometry conversion

`Geometry` converts font-specific contours into primitives understood by the distance-field generator.

```text
FontContour
     ↓
Contour
     ↓
Edge[]
    ├─ LineSegment
    └─ QuadraticSegment
```

This layer owns TrueType contour semantics such as implied on-curve points.

The important boundary is:

> **FontReader understands fonts. Geometry understands curves.**

The distance-field generator should not need to know whether its curves originally came from TrueType, synthetic test geometry, or some future font format.

That gives us an independently testable MSDF implementation.

### Stage 8: kerning

Kerning follows working glyph geometry. It is required in the final `FontBuildResult`, but it does
not block proving the reader, contour handling, or MSDF pipeline.

NAP v1 reads legacy `kern` and OpenType GPOS Pair Adjustment data through independent parsers. The
rest of Typography consumes one representation regardless of source:

```text
(left glyph index, right glyph index) → advance adjustment
```

The final output currently stores `TextKerningPair` values keyed by codepoint pairs. The reader
translates selected glyph-index pairs through the requested repertoire and divides their
font-unit adjustment by `unitsPerEm` to match the output's em-space advances. Neither `CmapTable`
nor `GlyphReader` owns kerning parsing.

The legacy parser accepts version-zero `kern` tables with horizontal format-zero pair subtables.
Ordinary subtables add; override subtables replace earlier values for duplicate pairs. Vertical,
minimum, and cross-stream subtables are ignored. Unsupported horizontal formats, malformed pairs,
and unsupported table versions produce an explicit diagnostic.

The GPOS parser is intentionally limited to the path needed for the `kern` feature:

```text
ScriptList / FeatureList / LookupList
    → kern feature
    → Pair Adjustment lookup
        → PairPos Format 1 (explicit glyph pairs)
        → PairPos Format 2 (class-based pairs)
```

This is not a general OpenType layout or shaping engine. NAP v1 supports GPOS 1.0 and 1.1 without
FeatureVariations, lookup type 2 with zero lookup flags, and PairPos formats 1 and 2. It applies the
sum of the two ValueRecords' `xAdvance` fields; placement and device adjustments are not represented
by `TextKerningPair`. Other lookup types are ignored, while unsupported formats in a selected
PairPos lookup and nonzero lookup flags produce an explicit diagnostic.

The default script selection is `latn`, falling back to `DFLT` when absent. A supplied language tag
selects that language system, falling back to the script's default LangSys; without a language tag,
the default LangSys is used, or the first listed language system when no default exists. If the
selected `kern` feature references supported PairPos data with `xAdvance`, use GPOS even when the
requested repertoire has no matching pairs. Otherwise use legacy `kern`. Never sum the two sources.

---

## 8. Distance-field generation

The distance-field subsystem receives geometry and generates an RGB glyph image.

```text
Contour[]
    ↓
Edge coloring
    ↓
Distance evaluation
    ↓
RGB distance field
```

It owns:

```text
edge coloring
signed-distance evaluation
inside/outside determination
distance normalization
RGB channel generation
generation resolution
distance range
padding
```

It does **not** own atlas placement.

This separation is important because an individual glyph should be testable without generating an entire font.

Stage 10 implements this boundary with `MsdfGenerator.Generate(contours, settings)`. Settings specify
pixels per geometry unit, the encoded distance range in pixels, and bitmap padding. The generated
`GlyphBitmap` contains width, height, and row-major RGB8 bytes. Contours with at least three detected
corners receive deterministic cycling RGB edge masks; smooth contours use all channels. Each channel
stores its nearest assigned-edge distance, signed by the shape's nonzero-winding fill result, then
normalized around 128 (inside above the midpoint, outside below it). The generator handles one glyph
only and does not perform atlas placement or texture upload.

`MsdfGeneratorTests` generates a synthetic capital A with a reversed-winding counter, checks the
inside/counter values and channel variation, and writes `msdf-A.ppm` beside the test assembly as a
trivial visual diagnostic.

---

## 9. Atlas generation

Atlas generation receives generated glyph bitmaps:

```text
GlyphBitmap[]
      ↓
FontAtlasBuilder
      ↓
FontAtlas
+ glyph placements
```

It owns:

```text
packing
atlas dimensions
pixel placement
atlas bounds
```

It must be deterministic: identical source font, repertoire, and generation settings must produce identical output.

The resulting atlas currently needs RGB8 data; `FontBuildResult` already identifies the atlas as `rgb8`. 

---

## 10. Typography orchestration

The top-level Typography processor composes the stages:

```text
TypographyProcessor

Read font
    ↓
Resolve requested codepoints
    ↓
Extract required glyphs
    ↓
Convert outlines
    ↓
Generate glyph distance fields
    ↓
Pack atlas
    ↓
Construct FontBuildResult
```

The runtime-facing Typography boundary is `IFontBuilder`:

```csharp
FontBuildResult Build(
    string sourcePath,
    IReadOnlyList<int> codepoints,
    FontGenerationSettings settings);
```

`FontBuilder` composes these stages and returns the complete `FontBuildResult`. The runtime owns style-specific generation settings, atlas upload/caching, and the resulting `ITextStyle`. NAP owns only source-path validation, file copying, and manifest registration.

---

## 11. Dependency requirements

`Nexus.Assets/Typography` should remain a **self-contained managed implementation**.

Architectural requirements:

* No native DLLs.
* No P/Invoke.
* No external executables.
* No installed system fonts.
* No runtime Nexus dependency.
* No dependency on a general-purpose font framework.
* No dependence on platform graphics APIs.
* No temporary intermediate interchange format.
* Deterministic operation from source bytes and build settings.

Standard .NET libraries are sufficient infrastructure.

The typography stages follow these requirements: font generation is managed, with no native generator, P/Invoke boundary, or native font-library dependency.

---

## 12. Scope philosophy

The governing rule should be:

> **Implement the font features required to realize Nexus text styles, not the features expected of a general-purpose font library.**

That means unsupported features are acceptable.

Incorrect interpretation is not.

For example, if runtime typography supports TrueType quadratic outlines but not CFF outlines, encountering CFF should result in a clear unsupported-font diagnostic rather than incorrect output.

```text
Font 'foo.otf' uses CFF outlines, which are not supported by Nexus Typography.
```

rather than trying to approximate them.

The same principle applies as we encounter shaping, variation, advanced positioning, color fonts, hinting, or other OpenType functionality.

---

## 13. Testing architecture

The stage separation gives us unusually good testing boundaries:

```text
FontReader tests
    known bytes → known tables/metrics

cmap tests
    codepoint → expected glyph index

glyph tests
    glyph → expected contours/points

composite glyph tests
    component transforms → expected outline

geometry tests
    font points → expected line/quadratic edges

distance tests
    synthetic edge → known signed distances

MSDF tests
    synthetic shape → deterministic RGB bitmap

atlas tests
    known rectangles → deterministic placement

integration tests
    known TTF + repertoire
        ↓
    deterministic FontBuildResult
```

The final integration test should not merely check that files exist. It should verify that the generated `FontBuildResult` satisfies the data actually consumed by `TextSpan`: scaling from `EmSize`, advances and kerning for pen placement, `PlaneBounds` for geometry, and `AtlasBounds` for texture coordinates. 

That gives us a clean architectural spec to hand to the coding agent. The next spec can then be much more mechanical: **NAP Typography TrueType Reader v1**, defining the supported SFNT structures, byte order, required tables, table parsing order, glyph resolution, simple/composite `glyf` decoding, coordinate reconstruction, and explicit unsupported cases.
