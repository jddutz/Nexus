namespace Nexus.Assets.Fonts;

/// <summary>
/// Describes the code point, layout, and atlas bounds of one glyph.
/// </summary>
/// <param name="Codepoint">The Unicode code point represented by the glyph.</param>
/// <param name="Advance">The horizontal advance in font units.</param>
/// <param name="PlaneBounds">The glyph bounds in font layout coordinates.</param>
/// <param name="AtlasBounds">The glyph bounds in atlas pixel coordinates.</param>
public readonly record struct FontGlyph(
    int Codepoint,
    double Advance,
    FontBounds PlaneBounds,
    FontBounds AtlasBounds
);
