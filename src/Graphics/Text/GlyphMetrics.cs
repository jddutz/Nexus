namespace Nexus.Graphics.Text;

/// <summary>
/// Describes the code point, layout, and atlas bounds of one glyph.
/// </summary>
public readonly record struct FontGlyph(
    int Codepoint,
    double Advance,
    FontBounds PlaneBounds,
    FontBounds AtlasBounds
);