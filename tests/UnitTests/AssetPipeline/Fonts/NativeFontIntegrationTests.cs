using Nexus.AssetPipeline.Fonts;

namespace Nexus.AssetPipeline.Tests;

public sealed class NativeFontIntegrationTests
{
    [Fact]
    public void NativeRasterizer_generatesAtlasArtifact()
    {
        // Native binaries and a licensed test font are supplied by the native CI job.
        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("NAP_RUN_NATIVE_FONT_TESTS"),
                "1",
                StringComparison.Ordinal
            )
        )
            return;

        var fontPath =
            Environment.GetEnvironmentVariable("NAP_TEST_FONT_PATH")
            ?? throw new InvalidOperationException(
                "NAP_TEST_FONT_PATH must name an explicitly supplied licensed .ttf or .otf file."
            );
        Assert.False(string.IsNullOrWhiteSpace(fontPath));
        Assert.True(File.Exists(fontPath), $"The test font '{fontPath}' does not exist.");
        var settings = new FontGenerationSettings();
        var codepoints = new FontGlyphRepertoire().GetCodepoints();
        var result = new NativeFontRasterizer().Rasterize(fontPath, codepoints, settings);

        Assert.True(result.Atlas.Width > 0);
        Assert.True(result.Atlas.Height > 0);
        Assert.Equal(result.Atlas.Width * result.Atlas.Height * 3, result.Atlas.Pixels.Length);
        Assert.Contains(result.Atlas.Pixels, value => value != 0);
        Assert.Contains(result.Glyphs, glyph => glyph.Codepoint == 'A' && glyph.Advance > 0);
        Assert.All(
            result.Glyphs,
            glyph => Assert.True(glyph.AtlasBounds.Right >= glyph.AtlasBounds.Left)
        );

        var package = Path.Combine(Path.GetTempPath(), $"nap-native-{Guid.NewGuid():N}");
        try
        {
            var atlasPath = Path.Combine(package, "atlas.rgb8");
            FontAtlasWriter.Write(atlasPath, result);
            Assert.True(File.Exists(Path.Combine(package, "atlas.rgb8")));
            Assert.False(File.Exists(Path.Combine(package, "font.json")));
        }
        finally
        {
            if (Directory.Exists(package))
                Directory.Delete(package, true);
        }
    }
}
