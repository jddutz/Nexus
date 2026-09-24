using Nexus.AssetPipeline.Fonts;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies that the managed typography stages produce a complete runtime font result.
/// </summary>
public sealed class ManagedFontRasterizerTests
{
    /// <summary>
    /// Builds selected glyphs and checks metrics, geometry, atlas bounds, and RGB8 data.
    /// </summary>
    [Fact]
    public void Rasterize_assemblesManagedFontBuildResult()
    {
        var fontPath = FindTestFont();
        if (fontPath is null)
            return;

        var result = new ManagedFontRasterizer().Rasterize(
            fontPath,
            ['A', 'V', ' '],
            new FontGenerationSettings
            {
                EmSize = 32,
                DistanceRange = 4,
                Padding = 2,
            }
        );

        Assert.Equal(32, result.Metrics.EmSize);
        Assert.True(result.Metrics.Ascender > 0);
        Assert.True(result.Metrics.Descender < 0);
        Assert.True(result.Metrics.LineHeight > 0);
        Assert.Equal(new MsdfMetadata(4, 32), result.Msdf);
        Assert.Equal(['A', 'V', ' '], result.Glyphs.Select(glyph => glyph.Codepoint));
        Assert.All(result.Glyphs, glyph => Assert.True(glyph.Advance > 0));

        var glyphA = Assert.Single(result.Glyphs, glyph => glyph.Codepoint == 'A');
        Assert.True(glyphA.PlaneBounds.Right > glyphA.PlaneBounds.Left);
        Assert.True(glyphA.PlaneBounds.Top > glyphA.PlaneBounds.Bottom);
        Assert.True(glyphA.AtlasBounds.Right > glyphA.AtlasBounds.Left);
        Assert.True(glyphA.AtlasBounds.Top > glyphA.AtlasBounds.Bottom);
        Assert.All(
            result.Glyphs.Where(glyph => glyph.Codepoint != ' '),
            glyph =>
            {
                Assert.InRange(glyph.AtlasBounds.Left, 0, result.Atlas.Width);
                Assert.InRange(glyph.AtlasBounds.Right, 0, result.Atlas.Width);
                Assert.InRange(glyph.AtlasBounds.Bottom, 0, result.Atlas.Height);
                Assert.InRange(glyph.AtlasBounds.Top, 0, result.Atlas.Height);
            }
        );
        Assert.Equal(result.Atlas.Width * result.Atlas.Height * 3, result.Atlas.Pixels.Length);
        Assert.Contains(result.Atlas.Pixels, value => value != 0);
    }

    /// <summary>
    /// Finds an explicitly supplied or repository-local font for integration testing.
    /// </summary>
    /// <returns>The font path, or null when local font assets are unavailable.</returns>
    private static string? FindTestFont()
    {
        var configuredPath = Environment.GetEnvironmentVariable("NAP_TEST_FONT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var fontDirectory = Path.Combine(directory.FullName, ".assets", "Fonts");
            if (Directory.Exists(fontDirectory))
                return Directory
                    .GetFiles(fontDirectory, "*.ttf")
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault();
        }

        return null;
    }
}
