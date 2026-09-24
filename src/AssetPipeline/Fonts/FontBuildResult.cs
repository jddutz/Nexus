namespace Nexus.AssetPipeline.Fonts;

/// <summary>
/// Contains all generated data required to write and describe a font asset.
/// </summary>
/// <param name="Atlas">The generated RGB8 atlas.</param>
/// <param name="Metrics">The font-wide metrics.</param>
/// <param name="Glyphs">The generated glyph metrics and bounds.</param>
/// <param name="Kerning">The kerning pairs for requested glyphs.</param>
/// <param name="Msdf">The parameters needed to interpret the MSDF atlas.</param>
public sealed record FontBuildResult(
    FontAtlas Atlas,
    FontMetrics Metrics,
    IReadOnlyList<FontGlyph> Glyphs,
    IReadOnlyList<TextKerningPair> Kerning,
    MsdfMetadata Msdf
);

/// <summary>
/// Contains the generated atlas pixels and the dimensions needed to interpret them.
/// </summary>
/// <param name="Width">The atlas width in pixels.</param>
/// <param name="Height">The atlas height in pixels.</param>
/// <param name="Pixels">The row-major RGB8 pixel data.</param>
public sealed record FontAtlas(int Width, int Height, byte[] Pixels)
{
    public const string PixelFormat = "rgb8";
}

/// <summary>
/// Contains the MSDF parameters needed to interpret a generated font atlas.
/// </summary>
/// <param name="DistanceRange">The generated signed-distance range.</param>
/// <param name="GenerationEmSize">The generation resolution in pixels per em.</param>
public sealed record MsdfMetadata(double DistanceRange, int GenerationEmSize);
