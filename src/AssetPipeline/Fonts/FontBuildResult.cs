namespace Nexus.AssetPipeline.Fonts;

public sealed record FontBuildResult(
    FontAtlas Atlas,
    FontMetrics Metrics,
    IReadOnlyList<FontGlyph> Glyphs,
    IReadOnlyList<FontKerningPair> Kerning,
    MsdfMetadata Msdf
);

public sealed record FontAtlas(int Width, int Height, byte[] Pixels)
{
    public const string PixelFormat = "rgb8";
}

public sealed record MsdfMetadata(double DistanceRange, int GenerationEmSize);
