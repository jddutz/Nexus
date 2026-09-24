namespace Nexus.AssetPipeline.Fonts;

public sealed record FontBuildResult(
    FontAtlas Atlas,
    FontMetrics Metrics,
    IReadOnlyList<FontGlyph> Glyphs,
    IReadOnlyList<TextKerningPair> Kerning,
    MsdfMetadata Msdf
);

/// <summary>
/// Describes the runtime font metadata stored in the content manifest.
/// </summary>
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
public sealed record FontManifestAtlas(string FilePath, int Width, int Height, string Format);

/// <summary>
/// Contains the generated atlas pixels and the dimensions needed to interpret them.
/// </summary>
public sealed record FontAtlas(int Width, int Height, byte[] Pixels)
{
    public const string PixelFormat = "rgb8";
}

/// <summary>
/// Contains the MSDF parameters needed to interpret a generated font atlas.
/// </summary>
public sealed record MsdfMetadata(double DistanceRange, int GenerationEmSize);
