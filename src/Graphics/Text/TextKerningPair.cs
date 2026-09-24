namespace Nexus.Graphics.Text;

/// <summary>
/// Identifies an ordered pair of glyph code points with a kerning adjustment.
/// </summary>
public readonly record struct TextKerningPair(
	int LeftCodepoint,
	int RightCodepoint,
	double AdvanceAdjustment
);