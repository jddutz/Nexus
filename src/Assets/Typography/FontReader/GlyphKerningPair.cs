namespace Nexus.Assets.Typography.FontReader;

/// <summary>
/// Represents a horizontal advance adjustment for an ordered pair of glyph indices.
/// </summary>
public readonly record struct GlyphKerningPair
{
    /// <summary>
    /// Initializes a glyph-index kerning pair.
    /// </summary>
    /// <param name="leftGlyphIndex">The glyph on the left side of the pair.</param>
    /// <param name="rightGlyphIndex">The glyph on the right side of the pair.</param>
    /// <param name="advanceAdjustment">The adjustment in font units.</param>
    public GlyphKerningPair(ushort leftGlyphIndex, ushort rightGlyphIndex, int advanceAdjustment)
    {
        LeftGlyphIndex = leftGlyphIndex;
        RightGlyphIndex = rightGlyphIndex;
        AdvanceAdjustment = advanceAdjustment;
    }

    /// <summary>
    /// Gets the glyph index on the left side of the pair.
    /// </summary>
    public ushort LeftGlyphIndex { get; }

    /// <summary>
    /// Gets the glyph index on the right side of the pair.
    /// </summary>
    public ushort RightGlyphIndex { get; }

    /// <summary>
    /// Gets the horizontal advance adjustment in font units.
    /// </summary>
    public int AdvanceAdjustment { get; }
}
