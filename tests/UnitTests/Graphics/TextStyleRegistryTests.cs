using Microsoft.Extensions.Configuration;
using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Text;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies generated font atlases are converted to supported runtime texture formats.</summary>
public sealed class TextStyleRegistryTests
{
    /// <summary>Verifies RGB atlas channels are preserved and opaque alpha is added to RGBA8 data.</summary>
    [Fact]
    public void GetOrCreate_convertsRgbAtlasToRgbaTexture()
    {
        var font = new FontBuildResult(
            new FontAtlas(2, 1, [17, 29, 43, 51, 61, 71]),
            new FontMetrics(16, 12, -4, 16),
            [],
            [],
            new MsdfMetadata(4, 16)
        );
        var manifest = new ContentManifest(
            string.Empty,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Fonts:Content:test-font:FilePath"] = "test-font.ttf",
                    }
                )
                .Build()
        );
        using var registry = new TextStyleRegistry(manifest, new StubFontBuilder(font));

        var style = registry.GetOrCreate(new TextStyleDescription("Test", "test-font", 16));
        Span<byte> pixels = stackalloc byte[8];

        Assert.Equal(ColorFormatEnum.RGBA8UNorm, style.Texture.TextureFormat);
        style.Texture.WriteTo(0, 2, style.Texture.TextureFormat, pixels);
        Assert.Equal(new byte[] { 17, 29, 43, 255, 51, 61, 71, 255 }, pixels.ToArray());
    }

    /// <summary>Provides one fixed font result to the text-style registry test.</summary>
    private sealed class StubFontBuilder(FontBuildResult result) : IFontBuilder
    {
        /// <inheritdoc />
        public FontBuildResult Build(
            string sourcePath,
            IReadOnlyList<int> codepoints,
            FontGenerationSettings settings
        ) => result;
    }
}
