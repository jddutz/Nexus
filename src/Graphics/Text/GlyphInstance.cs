namespace Nexus.Graphics.Text;

/// <summary>Represents a glyph positioned and colored for text rendering.</summary>
/// <param name="Glyph">The glyph metrics and atlas bounds to render.</param>
/// <param name="Position">The glyph's baseline origin in text-local GUI coordinates.</param>
/// <param name="Color">The color applied to this glyph.</param>
public readonly record struct GlyphInstance(FontGlyph Glyph, Vector2D<float> Position, Color Color);
