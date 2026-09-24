namespace Nexus.AssetPipeline;

using Nexus.AssetPipeline.Fonts;

/// <summary>
/// Describes the runtime font metadata stored in the content manifest.
/// </summary>
/// <param name="Atlas">The atlas artifact referenced by this font.</param>
/// <param name="Metrics">The font-wide metrics.</param>
/// <param name="Glyphs">The generated glyph metrics and bounds.</param>
/// <param name="Kerning">The kerning pairs for generated glyphs.</param>
/// <param name="Msdf">The parameters needed to interpret the MSDF atlas.</param>
public sealed record FontManifestEntry(
    FontManifestAtlas Atlas,
    FontMetrics Metrics,
    IReadOnlyList<FontGlyph> Glyphs,
    IReadOnlyList<TextKerningPair> Kerning,
    MsdfMetadata Msdf
);

/// <summary>
/// Describes the atlas artifact referenced by a runtime font manifest entry.
/// </summary>
/// <param name="FilePath">The atlas path relative to the content root.</param>
/// <param name="Width">The atlas width in pixels.</param>
/// <param name="Height">The atlas height in pixels.</param>
/// <param name="Format">The atlas pixel format.</param>
public sealed record FontManifestAtlas(string FilePath, int Width, int Height, string Format);
