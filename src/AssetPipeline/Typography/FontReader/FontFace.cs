using Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

namespace Nexus.AssetPipeline.Typography.FontReader;

/// <summary>
/// Represents font-wide metrics extracted from a font file.
/// </summary>
public sealed class FontFace
{
    /// <summary>
    /// Initializes a font face from its parsed global metric tables.
    /// </summary>
    /// <param name="head">The font header metrics.</param>
    /// <param name="hhea">The horizontal header metrics.</param>
    /// <param name="maxp">The maximum profile metrics.</param>
    internal FontFace(HeadTable head, HheaTable hhea, MaxpTable maxp)
    {
        UnitsPerEm = head.UnitsPerEm;
        IndexToLocFormat = head.IndexToLocFormat;
        Ascender = hhea.Ascender;
        Descender = hhea.Descender;
        LineGap = hhea.LineGap;
        NumberOfHorizontalMetrics = hhea.NumberOfHorizontalMetrics;
        GlyphCount = maxp.GlyphCount;
    }

    /// <summary>
    /// Gets the number of font units per em.
    /// </summary>
    public ushort UnitsPerEm { get; }

    /// <summary>
    /// Gets the typographic ascender in font units.
    /// </summary>
    public short Ascender { get; }

    /// <summary>
    /// Gets the typographic descender in font units.
    /// </summary>
    public short Descender { get; }

    /// <summary>
    /// Gets the typographic line gap in font units.
    /// </summary>
    public short LineGap { get; }

    /// <summary>
    /// Gets the number of glyphs in the font.
    /// </summary>
    public ushort GlyphCount { get; }

    /// <summary>
    /// Gets the number of full horizontal metric records in the font.
    /// </summary>
    public ushort NumberOfHorizontalMetrics { get; }

    /// <summary>
    /// Gets the format of glyph offsets in the loca table.
    /// </summary>
    public short IndexToLocFormat { get; }
}
