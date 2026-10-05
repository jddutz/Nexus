namespace Nexus.Assets.Fonts;

/// <summary>
/// Contains the source data consumed by the font rasterization pipeline.
/// </summary>
/// <param name="Metrics">The source font-wide metrics.</param>
/// <param name="GenerationSettings">The settings used by the NAP font rasterizer.</param>
/// <param name="Glyphs">The requested codepoints, glyph mappings, metrics, and outlines.</param>
/// <param name="Kerning">Supported kerning pairs for the requested codepoints.</param>
public sealed record FontRasterizerInput(
    FontRasterizerMetrics Metrics,
    FontRasterizerGenerationSettings GenerationSettings,
    IReadOnlyList<FontRasterizerGlyph> Glyphs,
    IReadOnlyList<TextKerningPair> Kerning
);

/// <summary>
/// Contains font-wide metrics decoded from the font file.
/// </summary>
/// <param name="UnitsPerEm">The number of font units per em.</param>
/// <param name="Ascender">The typographic ascender in font units.</param>
/// <param name="Descender">The typographic descender in font units.</param>
/// <param name="LineGap">The typographic line gap in font units.</param>
/// <param name="GlyphCount">The number of glyphs in the font.</param>
/// <param name="NumberOfHorizontalMetrics">The number of full horizontal metric records.</param>
/// <param name="IndexToLocFormat">The format of glyph offsets in the loca table.</param>
public sealed record FontRasterizerMetrics(
    ushort UnitsPerEm,
    short Ascender,
    short Descender,
    short LineGap,
    ushort GlyphCount,
    ushort NumberOfHorizontalMetrics,
    short IndexToLocFormat
);

/// <summary>
/// Contains the generation settings used to rasterize the font's glyphs.
/// </summary>
/// <param name="EmSize">The generation resolution in pixels per em.</param>
/// <param name="DistanceRange">The signed-distance range in pixels.</param>
/// <param name="Padding">The additional bitmap padding in pixels.</param>
public sealed record FontRasterizerGenerationSettings(
    int EmSize,
    double DistanceRange,
    int Padding
);

/// <summary>
/// Contains source layout metrics and rasterizer geometry for one mapped codepoint.
/// </summary>
/// <param name="Codepoint">The Unicode codepoint.</param>
/// <param name="GlyphIndex">The glyph index resolved for the codepoint.</param>
/// <param name="AdvanceWidth">The horizontal advance in font units.</param>
/// <param name="LeftSideBearing">The left side bearing in font units.</param>
/// <param name="Bounds">The exact contour bounds in font units, or null for an empty glyph.</param>
/// <param name="Contours">The converted line and quadratic contours in font units.</param>
public sealed record FontRasterizerGlyph(
    int Codepoint,
    ushort GlyphIndex,
    ushort AdvanceWidth,
    short LeftSideBearing,
    FontRasterizerBounds? Bounds,
    IReadOnlyList<FontRasterizerContour> Contours
);

/// <summary>
/// Describes axis-aligned contour bounds in font units.
/// </summary>
/// <param name="Left">The minimum horizontal coordinate.</param>
/// <param name="Bottom">The minimum vertical coordinate.</param>
/// <param name="Right">The maximum horizontal coordinate.</param>
/// <param name="Top">The maximum vertical coordinate.</param>
public sealed record FontRasterizerBounds(float Left, float Bottom, float Right, float Top);

/// <summary>
/// Contains the ordered curve segments forming one closed glyph contour.
/// </summary>
/// <param name="Segments">The ordered line or quadratic segments.</param>
public sealed record FontRasterizerContour(IReadOnlyList<FontRasterizerSegment> Segments);

/// <summary>
/// Describes one converted line or quadratic segment.
/// </summary>
/// <param name="Kind">The segment kind: <c>line</c> or <c>quadratic</c>.</param>
/// <param name="Start">The starting point in font units.</param>
/// <param name="Control">The quadratic control point, or null for a line.</param>
/// <param name="End">The ending point in font units.</param>
public sealed record FontRasterizerSegment(
    string Kind,
    FontRasterizerPoint Start,
    FontRasterizerPoint? Control,
    FontRasterizerPoint End
);

/// <summary>
/// Describes a two-dimensional point in font units.
/// </summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public sealed record FontRasterizerPoint(float X, float Y);
