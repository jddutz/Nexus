using Nexus.Graphics.Text;

namespace Nexus.AssetPipeline.Typography.Atlas;

/// <summary>
/// Contains a packed RGB8 atlas and the pixel-space bounds for each input glyph bitmap.
/// </summary>
/// <param name="Atlas">The packed atlas image.</param>
/// <param name="AtlasBounds">The bounds corresponding to the input bitmap order.</param>
public sealed record FontAtlasBuildResult(
    Fonts.FontAtlas Atlas,
    IReadOnlyList<FontBounds> AtlasBounds
);
