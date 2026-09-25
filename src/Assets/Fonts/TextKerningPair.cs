namespace Nexus.Assets.Fonts;

/// <summary>
/// Identifies an ordered pair of glyph code points with a kerning adjustment.
/// </summary>
/// <param name="LeftCodepoint">The code point of the first glyph.</param>
/// <param name="RightCodepoint">The code point of the second glyph.</param>
/// <param name="AdvanceAdjustment">The adjustment applied between the glyphs.</param>
public readonly record struct TextKerningPair(
    int LeftCodepoint,
    int RightCodepoint,
    double AdvanceAdjustment
);
