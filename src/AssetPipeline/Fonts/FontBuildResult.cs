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

public sealed record FontMetrics(double EmSize, double Ascender, double Descender, double LineHeight);

public sealed record FontGlyph(
    int Codepoint,
    double Advance,
    FontBounds PlaneBounds,
    FontBounds AtlasBounds
);

public sealed record FontBounds(double Left, double Bottom, double Right, double Top);
public sealed record FontKerningPair(int LeftCodepoint, int RightCodepoint, double AdvanceAdjustment);
public sealed record MsdfMetadata(double DistanceRange, int GenerationEmSize);
