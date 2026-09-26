namespace Nexus.Graphics.Text;

using Nexus.Assets.Fonts;

/// <summary>
/// Provides the font and visual data used to lay out and render text.
/// </summary>
public interface ITextStyle
{
    /// <summary>
    /// Gets the texture atlas containing the glyph images.
    /// </summary>
    ITexture Texture { get; }

    /// <summary>
    /// Gets the glyph metrics indexed by Unicode code point.
    /// </summary>
    IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

    /// <summary>
    /// Gets the metrics describing the font's coordinate system and vertical layout.
    /// </summary>
    FontMetrics FontMetrics { get; }

    /// <summary>
    /// Gets the parameters needed to interpret the font's MSDF atlas.
    /// </summary>
    MsdfMetadata Msdf { get; }

    /// <summary>
    /// Gets the kerning adjustments indexed by adjacent glyph code points.
    /// </summary>
    IReadOnlyDictionary<(int LeftCodepoint, int RightCodepoint), double> Kerning { get; }

    /// <summary>
    /// Gets the color applied to rendered glyphs.
    /// </summary>
    Color Color { get; }

    /// <summary>
    /// Gets the requested text size in the font's units.
    /// </summary>
    double Size { get; }
}
